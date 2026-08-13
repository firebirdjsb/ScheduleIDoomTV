#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>
#include <stdio.h>

#include "doomgeneric.h"
#include "doomkeys.h"

#ifdef _MSC_VER
#define strcasecmp _stricmp
#define strncasecmp _strnicmp
#define strdup _strdup
#endif

#define S1DOOM_EXPORT __declspec(dllexport)
#define KEYQUEUE_SIZE 128
#define FRAME_WIDTH DOOMGENERIC_RESX
#define FRAME_HEIGHT DOOMGENERIC_RESY
#define FRAME_BYTES (FRAME_WIDTH * FRAME_HEIGHT * 4)

static unsigned short s_key_queue[KEYQUEUE_SIZE];
static volatile LONG s_key_write = 0;
static volatile LONG s_key_read = 0;
static int s_initialized = 0;
static int s_active = 0;
static volatile LONG s_frame_counter = 0;
static volatile LONG s_last_exception = 0;
static char* s_wad_path_owned = NULL;
static char* s_argv[7] = { 0 };

void S1DoomAudioPause(void);
void S1DoomAudioResume(void);
int S1DoomAudioStatus(void);
void S1DoomAudioShutdown(void);

static void queue_key(int pressed, unsigned char key)
{
    LONG write = s_key_write;
    LONG next = (write + 1) % KEYQUEUE_SIZE;
    if (next == s_key_read)
        s_key_read = (s_key_read + 1) % KEYQUEUE_SIZE;
    s_key_queue[write] = (unsigned short)(((pressed ? 1 : 0) << 8) | key);
    MemoryBarrier();
    s_key_write = next;
}

static int prepare_arguments(const char* wad_path)
{
    if (s_wad_path_owned != NULL)
    {
        free(s_wad_path_owned);
        s_wad_path_owned = NULL;
    }
    s_wad_path_owned = strdup(wad_path);
    if (s_wad_path_owned == NULL)
        return 0;
    s_argv[0] = "ScheduleIDoom3TV";
    s_argv[1] = "-iwad";
    s_argv[2] = s_wad_path_owned;
    s_argv[3] = "-nogui";
    s_argv[4] = NULL;
    return 1;
}

void DG_Init(void)
{
    memset(s_key_queue, 0, sizeof(s_key_queue));
    s_key_write = 0;
    s_key_read = 0;
}
void DG_DrawFrame(void) { InterlockedIncrement(&s_frame_counter); }
void DG_SleepMs(uint32_t ms) { Sleep(ms); }
uint32_t DG_GetTicksMs(void) { return (uint32_t)GetTickCount64(); }

int DG_GetKey(int* pressed, unsigned char* doomKey)
{
    LONG read = s_key_read;
    if (read == s_key_write)
        return 0;
    unsigned short data = s_key_queue[read];
    s_key_read = (read + 1) % KEYQUEUE_SIZE;
    *pressed = (data >> 8) & 1;
    *doomKey = (unsigned char)(data & 0xff);
    return 1;
}

void DG_SetWindowTitle(const char* title) { (void)title; }

S1DOOM_EXPORT int __cdecl s1doom_create(const char* wad_path)
{
    if (s_initialized) { s_active = 1; return 1; }
    if (wad_path == NULL || wad_path[0] == '\0') return -1;
    DWORD attrs = GetFileAttributesA(wad_path);
    if (attrs == INVALID_FILE_ATTRIBUTES || (attrs & FILE_ATTRIBUTE_DIRECTORY)) return -2;
    if (!prepare_arguments(wad_path)) return -3;
    s_active = 1;
    s_last_exception = 0;
#if defined(_MSC_VER)
    __try { doomgeneric_Create(4, s_argv); }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        s_last_exception = (LONG)GetExceptionCode();
        s_active = 0;
        return -100;
    }
#else
    doomgeneric_Create(4, s_argv);
#endif
    s_initialized = 1;
    return 1;
}

S1DOOM_EXPORT int __cdecl s1doom_tick(void)
{
    if (!s_initialized || !s_active) return 0;
#if defined(_MSC_VER)
    __try { doomgeneric_Tick(); }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        s_last_exception = (LONG)GetExceptionCode();
        s_active = 0;
        return -100;
    }
#else
    doomgeneric_Tick();
#endif
    return 1;
}

S1DOOM_EXPORT void __cdecl s1doom_key(int pressed, unsigned char key)
{
    if (s_initialized && s_active) queue_key(pressed, key);
}

S1DOOM_EXPORT int __cdecl s1doom_copy_frame(unsigned char* rgba, int capacity, int* width, int* height, int* frame_number)
{
    if (width) *width = FRAME_WIDTH;
    if (height) *height = FRAME_HEIGHT;
    if (frame_number) *frame_number = (int)s_frame_counter;
    if (!s_initialized || DG_ScreenBuffer == NULL) return 0;
    if (rgba == NULL || capacity < FRAME_BYTES) return -1;
#if defined(_MSC_VER)
    __try
    {
#endif
        const uint32_t* src = (const uint32_t*)DG_ScreenBuffer;
        for (int y = 0; y < FRAME_HEIGHT; ++y)
        {
            int dstY = FRAME_HEIGHT - 1 - y;
            for (int x = 0; x < FRAME_WIDTH; ++x)
            {
                uint32_t p = src[y * FRAME_WIDTH + x];
                int d = (dstY * FRAME_WIDTH + x) * 4;
                rgba[d + 0] = (unsigned char)((p >> 16) & 0xff);
                rgba[d + 1] = (unsigned char)((p >> 8) & 0xff);
                rgba[d + 2] = (unsigned char)(p & 0xff);
                rgba[d + 3] = 0xff;
            }
        }
#if defined(_MSC_VER)
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        s_last_exception = (LONG)GetExceptionCode();
        s_active = 0;
        return -100;
    }
#endif
    return FRAME_BYTES;
}

S1DOOM_EXPORT void __cdecl s1doom_pause(void) { s_active = 0; S1DoomAudioPause(); }
S1DOOM_EXPORT void __cdecl s1doom_resume(void) { if (s_initialized) { s_active = 1; S1DoomAudioResume(); } }
S1DOOM_EXPORT int __cdecl s1doom_is_initialized(void) { return s_initialized; }
S1DOOM_EXPORT unsigned long __cdecl s1doom_last_exception(void) { return (unsigned long)s_last_exception; }
S1DOOM_EXPORT int __cdecl s1doom_audio_status(void) { return S1DoomAudioStatus(); }
S1DOOM_EXPORT void __cdecl s1doom_shutdown(void)
{
    s_active = 0;
    S1DoomAudioShutdown();
    if (DG_ScreenBuffer != NULL)
    {
        free(DG_ScreenBuffer);
        DG_ScreenBuffer = NULL;
    }
    if (s_wad_path_owned != NULL)
    {
        free(s_wad_path_owned);
        s_wad_path_owned = NULL;
    }
    s_initialized = 0;
    s_frame_counter = 0;
    s_key_write = 0;
    s_key_read = 0;
}
