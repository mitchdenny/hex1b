/*
 * hex1binterop.c - Native interop library for Hex1b terminal operations
 * 
 * This library provides PTY (pseudo-terminal) operations for Unix systems.
 * It properly spawns child processes attached to a pseudo-terminal with 
 * correct session and controlling terminal setup, which is required for
 * programs like tmux, screen, and other terminal multiplexers to work.
 * 
 * Key operations:
 * - hex1b_forkpty_shell() - Fork with PTY using forkpty() for shell spawning
 * - hex1b_resize() - Resize terminal dimensions
 * - hex1b_wait() - Wait for child process with timeout
 */

#define _GNU_SOURCE
#include <sys/types.h>
#include <sys/wait.h>
#include <sys/ioctl.h>
#include <unistd.h>
#include <fcntl.h>
#include <signal.h>
#include <errno.h>
#include <string.h>
#include <stdlib.h>
#include <stdio.h>
#include <termios.h>
#include <stdint.h>
#include <stddef.h>
#include <poll.h>
#include <pthread.h>
#include <time.h>

#ifdef __APPLE__
#include <util.h>
#else
#include <pty.h>
#endif

/* External environment variable */
extern char **environ;

struct hex1b_startup_state {
    int32_t ready;
    int32_t bytes_read;
    int32_t record_tag;
    int32_t record_error;
};

_Static_assert(sizeof(struct hex1b_startup_state) == 16, "startup ABI");
_Static_assert(sizeof(int) == sizeof(int32_t), "startup integer ABI");
_Static_assert(offsetof(struct hex1b_startup_state, ready) == 0, "ready ABI");
_Static_assert(offsetof(struct hex1b_startup_state, bytes_read) == 4, "bytes_read ABI");
_Static_assert(offsetof(struct hex1b_startup_state, record_tag) == 8, "record_tag ABI");
_Static_assert(offsetof(struct hex1b_startup_state, record_error) == 12, "record_error ABI");

static pthread_mutex_t spawn_mutex = PTHREAD_MUTEX_INITIALIZER;

static int protocol_error(int* error_stage)
{
    *error_stage = 4;
    errno = EPROTO;
    return -1;
}

int hex1b_poll_startup(int startup_fd, int timeout_ms,
    struct hex1b_startup_state* state, int* error_stage)
{
    if (error_stage != NULL) *error_stage = 4;
    if (startup_fd < 0 || timeout_ms < 0 || state == NULL || error_stage == NULL) {
        errno = EINVAL;
        return -1;
    }
    if ((state->ready != 0 && state->ready != 1) ||
        state->bytes_read < 0 || state->bytes_read >= 8)
        return protocol_error(error_stage);
    *error_stage = 0;
    struct pollfd descriptor = { .fd = startup_fd, .events = POLLIN };
    int result = poll(&descriptor, 1, timeout_ms);
    if (result < 0) {
        if (errno == EINTR) return 1;
        *error_stage = 4;
        return -1;
    }
    if (result == 0) return 1;
    if (descriptor.revents & POLLNVAL) {
        *error_stage = 4;
        errno = EBADF;
        return -1;
    }
    for (;;) {
        ssize_t count = read(startup_fd,
            (char*)state + offsetof(struct hex1b_startup_state, record_tag) + state->bytes_read,
            8 - state->bytes_read);
        if (count < 0) {
            if (errno == EINTR) continue;
            if (errno == EAGAIN || errno == EWOULDBLOCK) return 1;
            *error_stage = 4;
            return -1;
        }
        if (count == 0) {
            if (state->bytes_read != 0 || !state->ready)
                return protocol_error(error_stage);
            return 0;
        }
        state->bytes_read += (int32_t)count;
        if (state->bytes_read != 8) continue;
        int tag = state->record_tag;
        int error = state->record_error;
        state->bytes_read = 0;
        state->record_tag = 0;
        state->record_error = 0;
        if (tag == 1 && error == 0 && !state->ready) {
            state->ready = 1;
        } else if ((tag == 2 && !state->ready) || (tag == 3 && state->ready)) {
            if (error <= 0) return protocol_error(error_stage);
            *error_stage = tag;
            errno = error;
            return -1;
        } else {
            return protocol_error(error_stage);
        }
    }
}

int hex1b_abort_startup(int master_fd, int child_pid, int startup_fd)
{
    int cleanup_error = 0;
    /* Do not retry close: an interrupted close may already have released the fd. */
    if (startup_fd >= 0 && close(startup_fd) < 0) cleanup_error = errno;
    if (child_pid > 0) {
        int status;
        pid_t result;
        do { result = waitpid(child_pid, &status, WNOHANG); } while (result < 0 && errno == EINTR);
        if (result == 0) {
            if (kill(child_pid, SIGKILL) < 0 && errno != ESRCH && !cleanup_error)
                cleanup_error = errno;
            do { result = waitpid(child_pid, &status, 0); } while (result < 0 && errno == EINTR);
        }
        if (result < 0 && errno != ECHILD && !cleanup_error) cleanup_error = errno;
    }
    if (master_fd >= 0 && close(master_fd) < 0 && !cleanup_error) cleanup_error = errno;
    if (cleanup_error) {
        errno = cleanup_error;
        return -1;
    }
    return 0;
}

static int above_stdio(int fd)
{
    if (fd > STDERR_FILENO) return fd;
    int replacement = fcntl(fd, F_DUPFD_CLOEXEC, STDERR_FILENO + 1);
    int saved_error = errno;
    close(fd);
    errno = saved_error;
    return replacement;
}

static int startup_pipe(int descriptors[2])
{
#ifdef __APPLE__
    if (pipe(descriptors) < 0) return -1;
#else
    if (pipe2(descriptors, O_CLOEXEC) < 0) return -1;
#endif
    for (int i = 0; i < 2; ++i) {
        descriptors[i] = above_stdio(descriptors[i]);
        if (descriptors[i] < 0 ||
            fcntl(descriptors[i], F_SETFD, FD_CLOEXEC) < 0)
            return -1;
    }
    return fcntl(descriptors[0], F_SETFL, O_NONBLOCK);
}

static void write_record(int fd, const int32_t record[2])
{
    const char* cursor = (const char*)record;
    size_t remaining = 2 * sizeof(int32_t);
    while (remaining) {
        ssize_t count = write(fd, cursor, remaining);
        if (count < 0 && errno == EINTR) continue;
        if (count <= 0) _exit(127);
        cursor += count;
        remaining -= (size_t)count;
    }
}

static int spawn_start(const char* path, const char** argv, int argc,
    const char* working_dir, const char** envp, int width, int height,
    int* out_master_fd, int* out_child_pid, int* out_startup_fd, int* error_stage)
{
    if (out_master_fd != NULL) *out_master_fd = -1;
    if (out_child_pid != NULL) *out_child_pid = -1;
    if (out_startup_fd != NULL) *out_startup_fd = -1;
    if (error_stage != NULL) *error_stage = 1;
    if (path == NULL || argv == NULL || argc < 1 || envp == NULL ||
        out_master_fd == NULL || out_child_pid == NULL || out_startup_fd == NULL ||
        error_stage == NULL) {
        errno = EINVAL;
        return -1;
    }
    struct winsize ws = {
        .ws_row = height > 0 ? height : 24,
        .ws_col = width > 0 ? width : 80
    };
    struct sigaction sa_default;
    memset(&sa_default, 0, sizeof(sa_default));
    sa_default.sa_handler = SIG_DFL;
    sigemptyset(&sa_default.sa_mask);
    const int32_t ready[2] = { 1, 0 };
    int descriptors[2] = { -1, -1 };
    int master_fd = -1;
    pid_t pid = -1;
    int lock_error = pthread_mutex_lock(&spawn_mutex);
    if (lock_error) { errno = lock_error; return -1; }
    if (startup_pipe(descriptors) < 0) goto failed;
    pid = forkpty(&master_fd, NULL, NULL, &ws);
    if (pid < 0) goto failed;
    if (pid == 0) {
        close(descriptors[0]);
        for (int sig = 1; sig < NSIG; ++sig) {
            if (sig != SIGKILL && sig != SIGSTOP)
                sigaction(sig, &sa_default, NULL);
        }
        if (working_dir != NULL && working_dir[0] != '\0' && chdir(working_dir) < 0) {
            int32_t failure[2] = { 2, errno };
            write_record(descriptors[1], failure);
            _exit(127);
        }
        write_record(descriptors[1], ready);
        execve(path, (char* const*)argv, (char* const*)envp);
        int32_t failure[2] = { 3, errno };
        write_record(descriptors[1], failure);
        _exit(127);
    }
    master_fd = above_stdio(master_fd);
    if (master_fd < 0 || fcntl(master_fd, F_SETFD, FD_CLOEXEC) < 0) goto failed;
    {
        int result = close(descriptors[1]);
        descriptors[1] = -1;
        if (result < 0) goto failed;
    }
    pthread_mutex_unlock(&spawn_mutex);
    *out_master_fd = master_fd;
    *out_child_pid = pid;
    *out_startup_fd = descriptors[0];
    *error_stage = 0;
    return 0;
failed:
    {
        int saved_error = errno;
        if (descriptors[1] >= 0) close(descriptors[1]);
        pthread_mutex_unlock(&spawn_mutex);
        hex1b_abort_startup(master_fd, pid, descriptors[0]);
        errno = saved_error;
        return -1;
    }
}

int hex1b_forkpty_shell_env_start(const char* shell_path, const char* working_dir,
    const char** envp, int width, int height, int* out_master_fd, int* out_child_pid,
    int* out_startup_fd, int* error_stage)
{
    char login_shell_name[256];
    const char* shell_name = shell_path == NULL ? NULL : strrchr(shell_path, '/');
    shell_name = shell_name ? shell_name + 1 : shell_path;
    snprintf(login_shell_name, sizeof(login_shell_name), "-%s", shell_name ? shell_name : "");
    const char* argv[] = { login_shell_name, NULL };
    return spawn_start(shell_path, argv, 1, working_dir, envp, width, height,
        out_master_fd, out_child_pid, out_startup_fd, error_stage);
}

int hex1b_forkpty_exec_env_start(const char* exec_path, const char** argv, int argc,
    const char* working_dir, const char** envp, int width, int height,
    int* out_master_fd, int* out_child_pid, int* out_startup_fd, int* error_stage)
{
    return spawn_start(exec_path, argv, argc, working_dir, envp, width, height,
        out_master_fd, out_child_pid, out_startup_fd, error_stage);
}

static int finish_startup(int master_fd, int child_pid, int startup_fd,
    int* out_master_fd, int* out_child_pid)
{
    struct hex1b_startup_state state = {0};
    struct timespec start, now;
    if (clock_gettime(CLOCK_MONOTONIC, &start) < 0) goto failed;
    for (;;) {
        if (clock_gettime(CLOCK_MONOTONIC, &now) < 0) goto failed;
        int64_t elapsed = (int64_t)(now.tv_sec - start.tv_sec) * 1000 +
            (now.tv_nsec - start.tv_nsec) / 1000000;
        if (elapsed >= 10000) { errno = ETIMEDOUT; goto failed; }
        int stage;
        int result = hex1b_poll_startup(startup_fd, (int)(10000 - elapsed), &state, &stage);
        if (result < 0) goto failed;
        if (result == 0) break;
    }
    {
        int result = close(startup_fd);
        startup_fd = -1;
        if (result < 0) goto failed;
    }
    *out_master_fd = master_fd;
    *out_child_pid = child_pid;
    return 0;
failed:
    {
        int saved_error = errno;
        hex1b_abort_startup(master_fd, child_pid, startup_fd);
        errno = saved_error;
        return -1;
    }
}

size_t hex1b_termios_size(void)
{
    return sizeof(struct termios);
}

int hex1b_termios_get(int fd, void* buffer, size_t capacity)
{
    if (buffer == NULL || capacity < sizeof(struct termios)) {
        errno = EINVAL;
        return -1;
    }
    struct termios attributes = {0};
    if (tcgetattr(fd, &attributes) != 0)
        return -1;
    memcpy(buffer, &attributes, sizeof(attributes));
    return 0;
}

int hex1b_termios_make_raw(void* buffer, size_t capacity, int preserve_opost)
{
    if (buffer == NULL || capacity < sizeof(struct termios)) {
        errno = EINVAL;
        return -1;
    }
    struct termios attributes;
    memcpy(&attributes, buffer, sizeof(attributes));
    cfmakeraw(&attributes);
    if (preserve_opost)
        attributes.c_oflag |= OPOST;
    memcpy(buffer, &attributes, sizeof(attributes));
    return 0;
}

int hex1b_termios_set(int fd, const void* buffer, size_t capacity)
{
    if (buffer == NULL || capacity < sizeof(struct termios)) {
        errno = EINVAL;
        return -1;
    }
    struct termios attributes;
    memcpy(&attributes, buffer, sizeof(attributes));
    return tcsetattr(fd, TCSAFLUSH, &attributes);
}

int hex1b_get_window_pixel_size(int fd, int* pixel_width, int* pixel_height)
{
    if (pixel_width == NULL || pixel_height == NULL) {
        errno = EINVAL;
        return -1;
    }
    *pixel_width = 0;
    *pixel_height = 0;
    struct winsize size = {0};
    if (ioctl(fd, TIOCGWINSZ, &size) != 0)
        return -1;
    *pixel_width = size.ws_xpixel;
    *pixel_height = size.ws_ypixel;
    return 0;
}

/**
 * Spawns a shell process attached to a new PTY using forkpty().
 * This is a simplified API that handles all PTY setup internally.
 * 
 * @param shell_path    Path to the shell executable (e.g., "/bin/bash")
 * @param working_dir   Working directory for the child (NULL for current)
 * @param envp          Complete NULL-terminated environment for the child
 * @param width         Initial terminal width in columns
 * @param height        Initial terminal height in rows
 * @param out_master_fd Output: Master PTY file descriptor
 * @param out_child_pid Output: PID of the spawned child process
 * 
 * @return 0 on success, -1 on error (errno is set)
 */
int hex1b_forkpty_shell_env(
    const char* shell_path,
    const char* working_dir,
    const char** envp,
    int width,
    int height,
    int* out_master_fd,
    int* out_child_pid)
{
    if (out_master_fd != NULL) *out_master_fd = -1;
    if (out_child_pid != NULL) *out_child_pid = -1;
    if (out_master_fd == NULL || out_child_pid == NULL) { errno = EINVAL; return -1; }
    int master_fd, child_pid, startup_fd, stage;
    if (hex1b_forkpty_shell_env_start(shell_path, working_dir, envp, width, height,
        &master_fd, &child_pid, &startup_fd, &stage) < 0) return -1;
    return finish_startup(master_fd, child_pid, startup_fd, out_master_fd, out_child_pid);
}

/* Keep the original entry point for callers that intentionally inherit environ. */
int hex1b_forkpty_shell(
    const char* shell_path,
    const char* working_dir,
    int width,
    int height,
    int* out_master_fd,
    int* out_child_pid)
{
    return hex1b_forkpty_shell_env(shell_path, working_dir, (const char**)environ,
        width, height, out_master_fd, out_child_pid);
}

/**
 * Resizes the terminal associated with the given master PTY.
 * 
 * @param master_fd  Master file descriptor
 * @param width      New terminal width in columns
 * @param height     New terminal height in rows
 * 
 * @return 0 on success, -1 on error
 */
int hex1b_resize(int master_fd, int width, int height)
{
    struct winsize ws;
    ws.ws_row = height;
    ws.ws_col = width;
    ws.ws_xpixel = 0;
    ws.ws_ypixel = 0;
    return ioctl(master_fd, TIOCSWINSZ, &ws);
}

/**
 * Waits for a child process to exit with timeout.
 * 
 * @param pid        PID of the child process
 * @param timeout_ms Timeout in milliseconds (-1 for infinite)
 * @param out_status Output: Exit status
 * 
 * @return 0 on success (child exited), 1 on timeout, -1 on error
 */
int hex1b_wait(int pid, int timeout_ms, int* out_status)
{
    if (timeout_ms < 0) {
        /* Infinite wait */
        int status;
        if (waitpid(pid, &status, 0) < 0) {
            return -1;
        }
        if (out_status != NULL) {
            if (WIFEXITED(status)) {
                *out_status = WEXITSTATUS(status);
            } else if (WIFSIGNALED(status)) {
                *out_status = 128 + WTERMSIG(status);
            } else {
                *out_status = -1;
            }
        }
        return 0;
    }

    /* Poll with timeout */
    int elapsed = 0;
    while (elapsed < timeout_ms) {
        int status;
        int result = waitpid(pid, &status, WNOHANG);
        
        if (result < 0) {
            return -1;
        }
        
        if (result > 0) {
            /* Child exited */
            if (out_status != NULL) {
                if (WIFEXITED(status)) {
                    *out_status = WEXITSTATUS(status);
                } else if (WIFSIGNALED(status)) {
                    *out_status = 128 + WTERMSIG(status);
                } else {
                    *out_status = -1;
                }
            }
            return 0;
        }

        /* Sleep 10ms and try again */
        usleep(10000);
        elapsed += 10;
    }

    /* Timeout */
    return 1;
}

/**
 * Spawns an executable with arguments attached to a new PTY using forkpty().
 * Unlike hex1b_forkpty_shell, this runs an arbitrary command with arguments.
 * 
 * @param exec_path     Path to the executable
 * @param argv          NULL-terminated array of arguments (including argv[0])
 * @param argc          Number of arguments in argv (not including NULL terminator)
 * @param working_dir   Working directory for the child (NULL for current)
 * @param envp          Complete NULL-terminated environment for the child
 * @param width         Initial terminal width in columns
 * @param height        Initial terminal height in rows
 * @param out_master_fd Output: Master PTY file descriptor
 * @param out_child_pid Output: PID of the spawned child process
 * 
 * @return 0 on success, -1 on error (errno is set)
 */
int hex1b_forkpty_exec_env(
    const char* exec_path,
    const char** argv,
    int argc,
    const char* working_dir,
    const char** envp,
    int width,
    int height,
    int* out_master_fd,
    int* out_child_pid)
{
    if (out_master_fd != NULL) *out_master_fd = -1;
    if (out_child_pid != NULL) *out_child_pid = -1;
    if (out_master_fd == NULL || out_child_pid == NULL) { errno = EINVAL; return -1; }
    int master_fd, child_pid, startup_fd, stage;
    if (hex1b_forkpty_exec_env_start(exec_path, argv, argc, working_dir, envp, width, height,
        &master_fd, &child_pid, &startup_fd, &stage) < 0) return -1;
    return finish_startup(master_fd, child_pid, startup_fd, out_master_fd, out_child_pid);
}

int hex1b_forkpty_exec(
    const char* exec_path,
    const char** argv,
    int argc,
    const char* working_dir,
    int width,
    int height,
    int* out_master_fd,
    int* out_child_pid)
{
    return hex1b_forkpty_exec_env(exec_path, argv, argc, working_dir, (const char**)environ,
        width, height, out_master_fd, out_child_pid);
}
