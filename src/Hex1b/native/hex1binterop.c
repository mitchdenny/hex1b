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

#ifdef __APPLE__
#include <util.h>
#else
#include <pty.h>
#endif

/* External environment variable */
extern char **environ;

/**
 * Spawns a shell process attached to a new PTY using forkpty().
 * This is a simplified API that handles all PTY setup internally.
 * 
 * @param shell_path    Path to the shell executable (e.g., "/bin/bash")
 * @param working_dir   Working directory for the child (NULL for current)
 * @param width         Initial terminal width in columns
 * @param height        Initial terminal height in rows
 * @param out_master_fd Output: Master PTY file descriptor
 * @param out_child_pid Output: PID of the spawned child process
 * 
 * @return 0 on success, -1 on error (errno is set)
 */
int hex1b_forkpty_shell(
    const char* shell_path,
    const char* working_dir,
    int width,
    int height,
    int* out_master_fd,
    int* out_child_pid)
{
    if (shell_path == NULL || out_master_fd == NULL || out_child_pid == NULL) {
        errno = EINVAL;
        return -1;
    }

    /* Set up terminal size */
    struct winsize ws;
    ws.ws_row = height > 0 ? height : 24;
    ws.ws_col = width > 0 ? width : 80;
    ws.ws_xpixel = 0;
    ws.ws_ypixel = 0;

    int master_fd;
    pid_t pid = forkpty(&master_fd, NULL, NULL, &ws);

    if (pid == -1) {
        return -1;
    }

    if (pid == 0) {
        /* ========== CHILD PROCESS ========== */
        
        /* Reset signal handlers to default */
        struct sigaction sa_default;
        memset(&sa_default, 0, sizeof(sa_default));
        sa_default.sa_handler = SIG_DFL;
        
        for (int sig = 1; sig < NSIG; sig++) {
            if (sig == SIGKILL || sig == SIGSTOP) continue;
            sigaction(sig, &sa_default, NULL);
        }

        /* Change working directory if specified */
        if (working_dir != NULL && working_dir[0] != '\0') {
            if (chdir(working_dir) < 0) {
                /* Non-fatal - continue with current directory */
            }
        }

        /* Build argv for shell - use login shell format */
        char* shell_name = strrchr(shell_path, '/');
        shell_name = shell_name ? shell_name + 1 : (char*)shell_path;
        
        /* Prepend '-' to make it a login shell */
        char login_shell_name[256];
        snprintf(login_shell_name, sizeof(login_shell_name), "-%s", shell_name);

        char* argv[] = { login_shell_name, NULL };

        /* Execute the shell */
        execve(shell_path, argv, environ);

        /* If execve returns, it failed */
        _exit(127);
    }

    /* ========== PARENT PROCESS ========== */
    *out_master_fd = master_fd;
    *out_child_pid = pid;
    return 0;
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
 * @param width         Initial terminal width in columns
 * @param height        Initial terminal height in rows
 * @param out_master_fd Output: Master PTY file descriptor
 * @param out_child_pid Output: PID of the spawned child process
 * 
 * @return 0 on success, -1 on error (errno is set)
 */
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
    if (exec_path == NULL || argv == NULL || out_master_fd == NULL || out_child_pid == NULL) {
        errno = EINVAL;
        return -1;
    }

    /* Set up terminal size */
    struct winsize ws;
    ws.ws_row = height > 0 ? height : 24;
    ws.ws_col = width > 0 ? width : 80;
    ws.ws_xpixel = 0;
    ws.ws_ypixel = 0;

    int master_fd;
    pid_t pid = forkpty(&master_fd, NULL, NULL, &ws);

    if (pid == -1) {
        return -1;
    }

    if (pid == 0) {
        /* ========== CHILD PROCESS ========== */
        
        /* Reset signal handlers to default */
        struct sigaction sa_default;
        memset(&sa_default, 0, sizeof(sa_default));
        sa_default.sa_handler = SIG_DFL;
        
        for (int sig = 1; sig < NSIG; sig++) {
            if (sig == SIGKILL || sig == SIGSTOP) continue;
            sigaction(sig, &sa_default, NULL);
        }

        /* Change working directory if specified */
        if (working_dir != NULL && working_dir[0] != '\0') {
            if (chdir(working_dir) < 0) {
                /* Non-fatal - continue with current directory */
            }
        }

        /* Execute with provided arguments - cast away const for execve */
        execve(exec_path, (char* const*)argv, environ);

        /* If execve returns, it failed */
        _exit(127);
    }

    /* ========== PARENT PROCESS ========== */
    *out_master_fd = master_fd;
    *out_child_pid = pid;
    return 0;
}
