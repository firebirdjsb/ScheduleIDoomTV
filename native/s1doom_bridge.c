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

static void queue_key(int pressed, unsigned char key)
{
    LONG write = s_key_write;
    LONG next = (write + 1) % KEYQUEUE_SIZE;

    // Drop the oldest event if the queue is full rather than blocking Doom.
    if (next == s_key_read)
        s_key_read = (s_key_read + 1) % KEYQUEUE_SIZE;

    s_key_queue[write] = (unsigned short)(((pressed ? 1 : 0) << 8) | key);
    MemoryBarrier();
    s_key_write = next;
}

void DG_Init(void)
{
    memset(s_key_queue, 0, sizeof(s_key_queue));
    s_key_write = 0;
    s_key_read = 0;
}

void DG_DrawFrame(void)
{
    InterlockedIncrement(&s_frame_counter);
}

void DG_SleepMs(uint32_t ms)
{
    Sleep(ms);
}

uint32_t DG_GetTicksMs(void)
{
    return (uint32_t)GetTickCount64();
}

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

void DG_SetWindowTitle(const char* title)
{
    (void)title;
}

S1DOOM_EXPORT int __cdecl s1doom_create(const char* wad_path)
{
    if (s_initialized)
    {
        s_active = 1;
        return 1;
    }

    if (wad_path == NULL || wad_path[0] == '\0')
        return -1;

    DWORD attrs = GetFileAttributesA(wad_path);
    if (attrs == INVALID_FILE_ATTRIBUTES || (attrs & FILE_ATTRIBUTE_DIRECTORY))
        return -2;

    char* argv[7];
    argv[0] = "ScheduleIDoomTV";
    argv[1] = "-iwad";
    argv[2] = (char*)wad_path;
    argv[3] = "-nosound";
    argv[4] = "-nomusic";
    argv[5] = "-nogui";
    argv[6] = NULL;

    s_active = 1;
    doomgeneric_Create(6, argv);
    s_initialized = 1;
    return 1;
}

S1DOOM_EXPORT int __cdecl s1doom_tick(void)
{
    if (!s_initialized || !s_active)
        return 0;

    doomgeneric_Tick();
    return 1;
}

S1DOOM_EXPORT void __cdecl s1doom_key(int pressed, unsigned char key)
{
    if (!s_initialized)
        return;
    queue_key(pressed, key);
}

S1DOOM_EXPORT int __cdecl s1doom_copy_frame(unsigned char* rgba, int capacity, int* width, int* height, int* frame_number)
{
    if (width) *width = FRAME_WIDTH;
    if (height) *height = FRAME_HEIGHT;
    if (frame_number) *frame_number = (int)s_frame_counter;

    if (!s_initialized || DG_ScreenBuffer == NULL)
        return 0;
    if (rgba == NULL || capacity < FRAME_BYTES)
        return -1;

    // DoomGeneric's default framebuffer stores 0x00RRGGBB. Convert to opaque RGBA
    // so Unity's RawImage cannot become transparent because Doom leaves alpha at 0.
    const uint32_t* src = (const uint32_t*)DG_ScreenBuffer;
    for (int i = 0; i < FRAME_WIDTH * FRAME_HEIGHT; ++i)
    {
        uint32_t p = src[i];
        rgba[i * 4 + 0] = (unsigned char)((p >> 16) & 0xff);
        rgba[i * 4 + 1] = (unsigned char)((p >> 8) & 0xff);
        rgba[i * 4 + 2] = (unsigned char)(p & 0xff);
        rgba[i * 4 + 3] = 0xff;
    }

    return FRAME_BYTES;
}

S1DOOM_EXPORT void __cdecl s1doom_pause(void)
{
    s_active = 0;
}

S1DOOM_EXPORT void __cdecl s1doom_resume(void)
{
    if (s_initialized)
        s_active = 1;
}

S1DOOM_EXPORT int __cdecl s1doom_is_initialized(void)
{
    return s_initialized;
}
