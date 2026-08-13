#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <mmsystem.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "doomtype.h"
#include "i_sound.h"
#include "memio.h"
#include "mus2mid.h"
#include "w_wad.h"
#include "z_zone.h"

#pragma comment(lib, "winmm.lib")

#define S1_MIX_RATE 44100
#define S1_MIX_CHANNELS 16
#define S1_MIX_BUFFER_COUNT 4
#define S1_MIX_BUFFER_FRAMES 1024

// Doom's config binding expects these variables whenever FEATURE_SOUND is
// enabled, even though this backend performs its own lightweight resampling.
int use_libsamplerate = 0;
float libsamplerate_scale = 0.65f;

typedef struct
{
    byte *samples;
    uint32_t length;
    uint64_t position;
    uint64_t step;
    int left;
    int right;
    int playing;
} s1_channel_t;

typedef struct
{
    char path[MAX_PATH];
} s1_song_t;

typedef struct
{
    char path[MAX_PATH];
    LONG generation;
    HANDLE stop_event;
    int looping;
} s1_music_request_t;

static HWAVEOUT s_wave = NULL;
static WAVEHDR s_headers[S1_MIX_BUFFER_COUNT];
static int16_t s_buffers[S1_MIX_BUFFER_COUNT][S1_MIX_BUFFER_FRAMES * 2];
static s1_channel_t s_channels[S1_MIX_CHANNELS];
static CRITICAL_SECTION s_audio_lock;
static int s_lock_ready = 0;
static volatile LONG s_audio_ready = 0;
static volatile LONG s_audio_stopping = 0;
static volatile LONG s_sfx_started = 0;
static boolean s_use_sfx_prefix = true;

static MCIDEVICEID s_music_device = 0;
static s1_song_t *s_current_song = NULL;
static int s_music_ready = 0;
static int s_music_looping = 0;
static int s_music_paused = 0;
static CRITICAL_SECTION s_music_lock;
static int s_music_lock_ready = 0;
static volatile LONG s_music_generation = 0;
static volatile LONG s_audio_paused = 0;
static volatile LONG s_music_workers = 0;
static volatile LONG s_music_playing = 0;
static HANDLE s_music_thread = NULL;
static HANDLE s_music_stop_event = NULL;

static int Clamp16(int value)
{
    if (value < -32768) return -32768;
    if (value > 32767) return 32767;
    return value;
}

static void FreeChannel(s1_channel_t *channel)
{
    if (channel->samples != NULL)
        free(channel->samples);
    memset(channel, 0, sizeof(*channel));
}

static void FillAudioBuffer(int16_t *output)
{
    int frame;
    int channel_index;

    EnterCriticalSection(&s_audio_lock);

    for (frame = 0; frame < S1_MIX_BUFFER_FRAMES; ++frame)
    {
        int left = 0;
        int right = 0;

        for (channel_index = 0; channel_index < S1_MIX_CHANNELS; ++channel_index)
        {
            s1_channel_t *channel = &s_channels[channel_index];
            uint32_t source_index;
            int sample;

            if (!channel->playing || channel->samples == NULL)
                continue;

            source_index = (uint32_t)(channel->position >> 16);
            if (source_index >= channel->length)
            {
                FreeChannel(channel);
                continue;
            }

            sample = ((int)channel->samples[source_index] - 128) << 8;
            left += (sample * channel->left) / 255;
            right += (sample * channel->right) / 255;
            channel->position += channel->step;
        }

        output[frame * 2] = (int16_t)Clamp16(left);
        output[frame * 2 + 1] = (int16_t)Clamp16(right);
    }

    LeaveCriticalSection(&s_audio_lock);
}

static void CALLBACK AudioCallback(HWAVEOUT wave, UINT message,
                                   DWORD_PTR instance, DWORD_PTR param1,
                                   DWORD_PTR param2)
{
    WAVEHDR *header;
    (void)instance;
    (void)param2;

    if (message != WOM_DONE || InterlockedCompareExchange(&s_audio_stopping, 0, 0))
        return;

    header = (WAVEHDR *)param1;
    FillAudioBuffer((int16_t *)header->lpData);
    waveOutWrite(wave, header, sizeof(*header));
}

static boolean S1_InitSound(boolean use_sfx_prefix)
{
    WAVEFORMATEX format;
    MMRESULT result;
    int i;

    s_use_sfx_prefix = use_sfx_prefix;
    memset(s_channels, 0, sizeof(s_channels));
    memset(s_headers, 0, sizeof(s_headers));
    InitializeCriticalSection(&s_audio_lock);
    s_lock_ready = 1;
    InterlockedExchange(&s_audio_stopping, 0);

    memset(&format, 0, sizeof(format));
    format.wFormatTag = WAVE_FORMAT_PCM;
    format.nChannels = 2;
    format.nSamplesPerSec = S1_MIX_RATE;
    format.wBitsPerSample = 16;
    format.nBlockAlign = format.nChannels * (format.wBitsPerSample / 8);
    format.nAvgBytesPerSec = format.nSamplesPerSec * format.nBlockAlign;

    result = waveOutOpen(&s_wave, WAVE_MAPPER, &format,
                         (DWORD_PTR)AudioCallback, 0, CALLBACK_FUNCTION);
    if (result != MMSYSERR_NOERROR)
    {
        DeleteCriticalSection(&s_audio_lock);
        s_lock_ready = 0;
        s_wave = NULL;
        return false;
    }

    InterlockedExchange(&s_audio_ready, 1);

    for (i = 0; i < S1_MIX_BUFFER_COUNT; ++i)
    {
        WAVEHDR *header = &s_headers[i];
        memset(s_buffers[i], 0, sizeof(s_buffers[i]));
        header->lpData = (LPSTR)s_buffers[i];
        header->dwBufferLength = sizeof(s_buffers[i]);
        if (waveOutPrepareHeader(s_wave, header, sizeof(*header)) != MMSYSERR_NOERROR)
            return false;
        waveOutWrite(s_wave, header, sizeof(*header));
    }

    return true;
}

static void S1_ShutdownSound(void)
{
    int i;

    if (!InterlockedCompareExchange(&s_audio_ready, 0, 0))
        return;

    InterlockedExchange(&s_audio_stopping, 1);
    InterlockedExchange(&s_audio_ready, 0);
    waveOutReset(s_wave);

    for (i = 0; i < S1_MIX_BUFFER_COUNT; ++i)
        waveOutUnprepareHeader(s_wave, &s_headers[i], sizeof(s_headers[i]));

    if (s_lock_ready)
    {
        EnterCriticalSection(&s_audio_lock);
        for (i = 0; i < S1_MIX_CHANNELS; ++i)
            FreeChannel(&s_channels[i]);
        LeaveCriticalSection(&s_audio_lock);
    }

    waveOutClose(s_wave);
    s_wave = NULL;

    if (s_lock_ready)
    {
        DeleteCriticalSection(&s_audio_lock);
        s_lock_ready = 0;
    }
}

static int S1_GetSfxLumpNum(sfxinfo_t *sfx)
{
    char name[9];

    if (sfx->link != NULL)
        sfx = sfx->link;

    if (s_use_sfx_prefix)
        _snprintf_s(name, sizeof(name), _TRUNCATE, "ds%s", sfx->name);
    else
        _snprintf_s(name, sizeof(name), _TRUNCATE, "%s", sfx->name);

    return W_GetNumForName(name);
}

static byte *LoadSoundSamples(sfxinfo_t *sfx, uint32_t *length,
                              uint32_t *sample_rate)
{
    byte *lump;
    byte *copy;
    int lump_length;
    uint32_t declared_length;

    lump = W_CacheLumpNum(sfx->lumpnum, PU_STATIC);
    lump_length = W_LumpLength(sfx->lumpnum);

    if (lump == NULL || lump_length < 8 || lump[0] != 0x03 || lump[1] != 0x00)
    {
        if (lump != NULL) W_ReleaseLumpNum(sfx->lumpnum);
        return NULL;
    }

    *sample_rate = (uint32_t)lump[2] | ((uint32_t)lump[3] << 8);
    declared_length = (uint32_t)lump[4]
                    | ((uint32_t)lump[5] << 8)
                    | ((uint32_t)lump[6] << 16)
                    | ((uint32_t)lump[7] << 24);

    if (*sample_rate == 0 || declared_length > (uint32_t)(lump_length - 8)
        || declared_length <= 48)
    {
        W_ReleaseLumpNum(sfx->lumpnum);
        return NULL;
    }

    *length = declared_length - 32;
    copy = (byte *)malloc(*length);
    if (copy != NULL)
        memcpy(copy, lump + 24, *length);

    W_ReleaseLumpNum(sfx->lumpnum);
    return copy;
}

static void S1_UpdateSoundParams(int channel_index, int volume, int separation)
{
    s1_channel_t *channel;
    int left;
    int right;

    if (!s_lock_ready || channel_index < 0 || channel_index >= S1_MIX_CHANNELS)
        return;

    left = ((254 - separation) * volume) / 127;
    right = (separation * volume) / 127;
    if (left < 0) left = 0;
    if (left > 255) left = 255;
    if (right < 0) right = 0;
    if (right > 255) right = 255;

    EnterCriticalSection(&s_audio_lock);
    channel = &s_channels[channel_index];
    channel->left = left;
    channel->right = right;
    LeaveCriticalSection(&s_audio_lock);
}

static int S1_StartSound(sfxinfo_t *sfx, int channel_index, int volume,
                         int separation)
{
    byte *samples;
    uint32_t length;
    uint32_t sample_rate;
    s1_channel_t *channel;

    if (!InterlockedCompareExchange(&s_audio_ready, 0, 0)
        || channel_index < 0 || channel_index >= S1_MIX_CHANNELS)
        return -1;

    samples = LoadSoundSamples(sfx, &length, &sample_rate);
    if (samples == NULL)
        return -1;

    EnterCriticalSection(&s_audio_lock);
    channel = &s_channels[channel_index];
    FreeChannel(channel);
    channel->samples = samples;
    channel->length = length;
    channel->position = 0;
    channel->step = ((uint64_t)sample_rate << 16) / S1_MIX_RATE;
    if (channel->step == 0) channel->step = 1;
    channel->playing = 1;
    InterlockedExchange(&s_sfx_started, 1);
    LeaveCriticalSection(&s_audio_lock);

    S1_UpdateSoundParams(channel_index, volume, separation);
    return channel_index;
}

static void S1_StopSound(int channel_index)
{
    if (!s_lock_ready || channel_index < 0 || channel_index >= S1_MIX_CHANNELS)
        return;

    EnterCriticalSection(&s_audio_lock);
    FreeChannel(&s_channels[channel_index]);
    LeaveCriticalSection(&s_audio_lock);
}

static boolean S1_SoundIsPlaying(int channel_index)
{
    boolean result;

    if (!s_lock_ready || channel_index < 0 || channel_index >= S1_MIX_CHANNELS)
        return false;

    EnterCriticalSection(&s_audio_lock);
    result = s_channels[channel_index].playing ? true : false;
    LeaveCriticalSection(&s_audio_lock);
    return result;
}

static void S1_UpdateSound(void) { }
static void S1_CacheSounds(sfxinfo_t *sounds, int count)
{
    (void)sounds;
    (void)count;
}

static snddevice_t s_sound_devices[] =
{
    SNDDEVICE_SB,
    SNDDEVICE_PAS,
    SNDDEVICE_GUS,
    SNDDEVICE_WAVEBLASTER,
    SNDDEVICE_SOUNDCANVAS,
    SNDDEVICE_AWE32
};

sound_module_t DG_sound_module =
{
    s_sound_devices,
    sizeof(s_sound_devices) / sizeof(s_sound_devices[0]),
    S1_InitSound,
    S1_ShutdownSound,
    S1_GetSfxLumpNum,
    S1_UpdateSound,
    S1_UpdateSoundParams,
    S1_StartSound,
    S1_StopSound,
    S1_SoundIsPlaying,
    S1_CacheSounds
};

static void CloseMusicDevice(void)
{
    DWORD wait_result;
    HANDLE thread = s_music_thread;
    HANDLE stop_event = s_music_stop_event;

    // MCI sequencer devices are most reliable when they are stopped and closed
    // by the same thread that opened them. Signal that owner and wait until it
    // has confirmed both commands before the native DLL can be unloaded.
    if (stop_event != NULL)
        SetEvent(stop_event);

    wait_result = thread != NULL
        ? WaitForSingleObject(thread, 15000)
        : WAIT_OBJECT_0;

    if (wait_result == WAIT_TIMEOUT)
    {
        // Last-resort interruption for a misbehaving MIDI driver. The worker
        // will still perform its own close when it returns.
        MCIDEVICEID device = s_music_device;
        if (device != 0)
        {
            mciSendCommandA(device, MCI_PAUSE, MCI_WAIT, 0);
            mciSendCommandA(device, MCI_STOP, MCI_WAIT, 0);
            mciSendCommandA(device, MCI_CLOSE, MCI_WAIT, 0);
        }
        if (thread != NULL)
            wait_result = WaitForSingleObject(thread, 5000);
    }

    if (thread != NULL && wait_result == WAIT_OBJECT_0)
        CloseHandle(thread);
    if (stop_event != NULL && wait_result == WAIT_OBJECT_0)
        CloseHandle(stop_event);

    if (wait_result == WAIT_OBJECT_0)
    {
        s_music_thread = NULL;
        s_music_stop_event = NULL;
    }

    if (s_music_lock_ready)
    {
        EnterCriticalSection(&s_music_lock);
        if (wait_result == WAIT_OBJECT_0)
            s_music_device = 0;
        s_music_paused = 0;
        LeaveCriticalSection(&s_music_lock);
    }

    if (wait_result == WAIT_OBJECT_0)
        InterlockedExchange(&s_music_playing, 0);
}

static boolean S1_InitMusic(void)
{
    if (!s_music_lock_ready)
    {
        InitializeCriticalSection(&s_music_lock);
        s_music_lock_ready = 1;
    }
    s_music_ready = 1;
    return true;
}

static void S1_ShutdownMusic(void)
{
    InterlockedIncrement(&s_music_generation);
    s_music_ready = 0;
    CloseMusicDevice();
}

static void S1_SetMusicVolume(int volume) { (void)volume; }

static void S1_PauseMusic(void)
{
    InterlockedExchange(&s_audio_paused, 1);
}

static void S1_ResumeMusic(void)
{
    InterlockedExchange(&s_audio_paused, 0);
}

static int WriteSongFile(const char *path, const void *data, size_t length)
{
    HANDLE file;
    DWORD written = 0;
    BOOL success;

    file = CreateFileA(path, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS,
                       FILE_ATTRIBUTE_TEMPORARY, NULL);
    if (file == INVALID_HANDLE_VALUE)
        return 0;

    success = WriteFile(file, data, (DWORD)length, &written, NULL);
    CloseHandle(file);
    return success && written == (DWORD)length;
}

static void *S1_RegisterSong(void *data, int length)
{
    s1_song_t *song;
    char temp_path[MAX_PATH];
    void *midi_data = data;
    size_t midi_length = (size_t)length;
    MEMFILE *input = NULL;
    MEMFILE *output = NULL;
    int converted = 0;

    if (!s_music_ready || data == NULL || length < 4)
        return NULL;

    song = (s1_song_t *)calloc(1, sizeof(*song));
    if (song == NULL)
        return NULL;

    if (GetTempPathA(sizeof(temp_path), temp_path) == 0
        || GetTempFileNameA(temp_path, "s1d", 0, song->path) == 0)
    {
        free(song);
        return NULL;
    }

    if (memcmp(data, "MUS\x1a", 4) == 0)
    {
        input = mem_fopen_read(data, (size_t)length);
        output = mem_fopen_write();
        // mus2mid follows the Doom convention: false means success.
        if (input == NULL || output == NULL || mus2mid(input, output))
            goto fail;
        mem_get_buf(output, &midi_data, &midi_length);
        converted = 1;
    }
    else if (memcmp(data, "MThd", 4) != 0)
    {
        goto fail;
    }

    if (!WriteSongFile(song->path, midi_data, midi_length))
        goto fail;

    if (input != NULL) mem_fclose(input);
    if (output != NULL) mem_fclose(output);
    (void)converted;
    return song;

fail:
    if (input != NULL) mem_fclose(input);
    if (output != NULL) mem_fclose(output);
    DeleteFileA(song->path);
    free(song);
    return NULL;
}

static void S1_StopSong(void)
{
    InterlockedIncrement(&s_music_generation);
    CloseMusicDevice();
    s_current_song = NULL;
}

static void S1_UnRegisterSong(void *handle)
{
    s1_song_t *song = (s1_song_t *)handle;
    if (song == NULL)
        return;
    if (song == s_current_song)
        S1_StopSong();
    DeleteFileA(song->path);
    free(song);
}

static DWORD WINAPI MusicOpenThread(LPVOID parameter)
{
    s1_music_request_t *request = (s1_music_request_t *)parameter;
    MCI_OPEN_PARMSA open_params;
    MCI_PLAY_PARMS play_params;
    MCI_STATUS_PARMS status_params;
    MCI_SEEK_PARMS seek_params;
    MCIDEVICEID device = 0;
    int paused = 0;
    int opened = 0;

    memset(&open_params, 0, sizeof(open_params));
    open_params.lpstrDeviceType = (LPCSTR)(DWORD_PTR)MCI_DEVTYPE_SEQUENCER;
    open_params.lpstrElementName = request->path;

    if (mciSendCommandA(0, MCI_OPEN,
                        MCI_OPEN_TYPE | MCI_OPEN_TYPE_ID | MCI_OPEN_ELEMENT | MCI_WAIT,
                        (DWORD_PTR)&open_params) != 0)
        goto done;

    device = open_params.wDeviceID;
    opened = 1;
    EnterCriticalSection(&s_music_lock);
    if (!s_music_ready || request->generation != s_music_generation
        || WaitForSingleObject(request->stop_event, 0) == WAIT_OBJECT_0)
    {
        LeaveCriticalSection(&s_music_lock);
        goto done;
    }

    s_music_device = device;
    memset(&play_params, 0, sizeof(play_params));
    if (mciSendCommandA(s_music_device, MCI_PLAY, 0,
                        (DWORD_PTR)&play_params) != 0)
    {
        s_music_device = 0;
        LeaveCriticalSection(&s_music_lock);
        goto done;
    }

    InterlockedExchange(&s_music_playing, 1);
    LeaveCriticalSection(&s_music_lock);

    while (WaitForSingleObject(request->stop_event, 10) == WAIT_TIMEOUT)
    {
        int should_pause = InterlockedCompareExchange(&s_audio_paused, 0, 0) != 0;

        if (should_pause && !paused)
        {
            if (mciSendCommandA(device, MCI_PAUSE, MCI_WAIT, 0) == 0)
                paused = 1;
        }
        else if (!should_pause && paused)
        {
            if (mciSendCommandA(device, MCI_RESUME, MCI_WAIT, 0) == 0)
                paused = 0;
        }

        if (paused || !request->looping)
            continue;

        memset(&status_params, 0, sizeof(status_params));
        status_params.dwItem = MCI_STATUS_MODE;
        if (mciSendCommandA(device, MCI_STATUS, MCI_STATUS_ITEM | MCI_WAIT,
                            (DWORD_PTR)&status_params) == 0
            && status_params.dwReturn == MCI_MODE_STOP)
        {
            memset(&seek_params, 0, sizeof(seek_params));
            memset(&play_params, 0, sizeof(play_params));
            mciSendCommandA(device, MCI_SEEK, MCI_SEEK_TO_START | MCI_WAIT,
                            (DWORD_PTR)&seek_params);
            mciSendCommandA(device, MCI_PLAY, 0,
                            (DWORD_PTR)&play_params);
        }
    }

done:
    if (opened)
    {
        // These commands deliberately run on the MCI owner thread. MCI_WAIT
        // guarantees that no sequencer notes survive after shutdown returns.
        mciSendCommandA(device, MCI_PAUSE, MCI_WAIT, 0);
        mciSendCommandA(device, MCI_STOP, MCI_WAIT, 0);
        mciSendCommandA(device, MCI_CLOSE, MCI_WAIT, 0);
    }

    if (s_music_lock_ready)
    {
        EnterCriticalSection(&s_music_lock);
        if (s_music_device == device)
            s_music_device = 0;
        s_music_paused = 0;
        LeaveCriticalSection(&s_music_lock);
    }

    InterlockedExchange(&s_music_playing, 0);
    free(request);
    InterlockedDecrement(&s_music_workers);
    return 0;
}

static void S1_PlaySong(void *handle, boolean looping)
{
    s1_song_t *song = (s1_song_t *)handle;
    s1_music_request_t *request;

    if (song == NULL)
        return;

    InterlockedIncrement(&s_music_generation);
    CloseMusicDevice();
    s_current_song = song;
    s_music_looping = looping ? 1 : 0;

    request = (s1_music_request_t *)calloc(1, sizeof(*request));
    if (request == NULL)
        return;
    strcpy_s(request->path, sizeof(request->path), song->path);
    request->generation = s_music_generation;
    request->looping = s_music_looping;
    request->stop_event = CreateEventA(NULL, TRUE, FALSE, NULL);
    if (request->stop_event == NULL)
    {
        free(request);
        return;
    }

    InterlockedIncrement(&s_music_workers);
    s_music_stop_event = request->stop_event;
    s_music_thread = CreateThread(NULL, 0, MusicOpenThread, request, 0, NULL);
    if (s_music_thread == NULL)
    {
        InterlockedDecrement(&s_music_workers);
        CloseHandle(s_music_stop_event);
        s_music_stop_event = NULL;
        free(request);
        return;
    }
}

static boolean S1_MusicIsPlaying(void)
{
    return InterlockedCompareExchange(&s_music_playing, 0, 0) ? true : false;
}

static void S1_PollMusic(void)
{
    // The owning MIDI worker performs pause/resume and loop polling so every
    // MCI command stays on the same thread as MCI_OPEN.
}

static snddevice_t s_music_devices[] =
{
    SNDDEVICE_SB,
    SNDDEVICE_ADLIB,
    SNDDEVICE_GUS,
    SNDDEVICE_GENMIDI,
    SNDDEVICE_SOUNDCANVAS
};

music_module_t DG_music_module =
{
    s_music_devices,
    sizeof(s_music_devices) / sizeof(s_music_devices[0]),
    S1_InitMusic,
    S1_ShutdownMusic,
    S1_SetMusicVolume,
    S1_PauseMusic,
    S1_ResumeMusic,
    S1_RegisterSong,
    S1_UnRegisterSong,
    S1_PlaySong,
    S1_StopSong,
    S1_MusicIsPlaying,
    S1_PollMusic
};

void S1DoomAudioPause(void)
{
    InterlockedExchange(&s_audio_paused, 1);
    if (s_wave != NULL) waveOutPause(s_wave);
}

void S1DoomAudioResume(void)
{
    InterlockedExchange(&s_audio_paused, 0);
    if (s_wave != NULL) waveOutRestart(s_wave);
}

int S1DoomAudioStatus(void)
{
    int status = 0;
    if (InterlockedCompareExchange(&s_audio_ready, 0, 0)) status |= 1;
    if (s_music_ready) status |= 2;
    if (s_music_device != 0) status |= 4;
    if (InterlockedCompareExchange(&s_sfx_started, 0, 0)) status |= 8;
    return status;
}

void S1DoomAudioShutdown(void)
{
    InterlockedExchange(&s_audio_paused, 1);
    S1_ShutdownMusic();
    S1_ShutdownSound();
}
