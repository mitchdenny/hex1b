// ptyspawn.c - Native PTY spawning with proper controlling terminal setup
// This is required for tmux, screen, and other programs that need a proper CTY.
//
// Build on Linux:
//   gcc -shared -fPIC -o libptyspawn.so ptyspawn.c -lutil
//
// Build on macOS:
//   clang -shared -fPIC -o libptyspawn.dylib ptyspawn.c

#define _GNU_SOURCE
#include <stdlib.h>
#include <stdio.h>
#include <string.h>
#include <unistd.h>
#include <fcntl.h>
#include <errno.h>
#include <signal.h>
#include <sys/ioctl.h>
#include <sys/wait.h>
#include <sys/types.h>
#include <termios.h>

#ifdef __linux__
#include <pty.h>
#include <utmp.h>
#elif defined(__APPLE__)
#include <util.h>
#endif

// Simple spawn that just runs a shell with no arguments
// This is for testing - takes just the shell path
// Returns 0 on success, -1 on error
int pty_forkpty_shell(const char *shell_path, const char *working_dir, 
                      int width, int height,
                      int *master_fd, int *child_pid) {
    struct winsize ws = {
        .ws_row = height,
        .ws_col = width,
        .ws_xpixel = 0,
        .ws_ypixel = 0
    };
    
    // Get default terminal settings, then disable OPOST (output processing).
    // This prevents LF->CRLF conversion which interferes with cursor positioning.
    // We keep ECHO and ICANON enabled so shells and tmux work properly.
    struct termios termios;
    memset(&termios, 0, sizeof(termios));
    
    // Start with sane defaults
    termios.c_iflag = ICRNL | IXON;           // CR->NL on input, enable XON/XOFF
    termios.c_oflag = 0;                       // DISABLE output processing (no OPOST/ONLCR)
    termios.c_cflag = CS8 | CREAD | CLOCAL;   // 8-bit chars, enable receiver, ignore modem
    termios.c_lflag = ECHO | ECHOE | ECHOK | ICANON | ISIG | IEXTEN;  // Normal input processing
    
    // Control characters
    termios.c_cc[VEOF] = 4;     // Ctrl+D
    termios.c_cc[VEOL] = 0;
    termios.c_cc[VERASE] = 127; // Backspace
    termios.c_cc[VINTR] = 3;    // Ctrl+C
    termios.c_cc[VKILL] = 21;   // Ctrl+U
    termios.c_cc[VMIN] = 1;
    termios.c_cc[VQUIT] = 28;   // Ctrl+backslash
    termios.c_cc[VSTART] = 17;  // Ctrl+Q
    termios.c_cc[VSTOP] = 19;   // Ctrl+S
    termios.c_cc[VSUSP] = 26;   // Ctrl+Z
    termios.c_cc[VTIME] = 0;
    
    // Set baud rate (required on some systems)
    cfsetispeed(&termios, B38400);
    cfsetospeed(&termios, B38400);
    
    int master;
    pid_t pid = forkpty(&master, NULL, &termios, &ws);
    
    if (pid < 0) {
        return -1;
    }
    
    if (pid == 0) {
        // Child process
        
        // Change to working directory
        if (working_dir && working_dir[0]) {
            chdir(working_dir);
        }
        
        // Reset signal handlers
        signal(SIGCHLD, SIG_DFL);
        signal(SIGHUP, SIG_DFL);
        signal(SIGINT, SIG_DFL);
        signal(SIGQUIT, SIG_DFL);
        signal(SIGTERM, SIG_DFL);
        signal(SIGALRM, SIG_DFL);
        
        // Build simple argv
        char *argv[] = { (char*)shell_path, NULL };
        
        // Execute
        execv(shell_path, argv);
        
        _exit(127);
    }
    
    // Parent
    *master_fd = master;
    *child_pid = pid;
    return 0;
}

// Alternative spawn using forkpty() - this is what 'script' uses
// Returns 0 on success (in parent with child_pid set), -1 on error
int pty_forkpty_spawn(const char *path, char *const argv[], char *const envp[],
                      const char *working_dir, int width, int height,
                      int *master_fd, int *child_pid) {
    struct winsize ws = {
        .ws_row = height,
        .ws_col = width,
        .ws_xpixel = 0,
        .ws_ypixel = 0
    };
    
    // Get default terminal settings, then disable OPOST (output processing).
    // This prevents LF->CRLF conversion which interferes with cursor positioning.
    // We keep ECHO and ICANON enabled so shells and tmux work properly.
    struct termios termios;
    memset(&termios, 0, sizeof(termios));
    
    termios.c_iflag = ICRNL | IXON;
    termios.c_oflag = 0;  // DISABLE output processing
    termios.c_cflag = CS8 | CREAD | CLOCAL;
    termios.c_lflag = ECHO | ECHOE | ECHOK | ICANON | ISIG | IEXTEN;
    
    termios.c_cc[VEOF] = 4;
    termios.c_cc[VEOL] = 0;
    termios.c_cc[VERASE] = 127;
    termios.c_cc[VINTR] = 3;
    termios.c_cc[VKILL] = 21;
    termios.c_cc[VMIN] = 1;
    termios.c_cc[VQUIT] = 28;
    termios.c_cc[VSTART] = 17;
    termios.c_cc[VSTOP] = 19;
    termios.c_cc[VSUSP] = 26;
    termios.c_cc[VTIME] = 0;
    
    cfsetispeed(&termios, B38400);
    cfsetospeed(&termios, B38400);
    
    int master;
    pid_t pid = forkpty(&master, NULL, &termios, &ws);
    
    if (pid < 0) {
        return -1;
    }
    
    if (pid == 0) {
        // Child process - forkpty already set up the PTY as controlling terminal
        
        // Change to working directory
        if (working_dir && working_dir[0]) {
            if (chdir(working_dir) < 0) {
                // Not fatal
            }
        }
        
        // Reset signal handlers
        signal(SIGCHLD, SIG_DFL);
        signal(SIGHUP, SIG_DFL);
        signal(SIGINT, SIG_DFL);
        signal(SIGQUIT, SIG_DFL);
        signal(SIGTERM, SIG_DFL);
        signal(SIGALRM, SIG_DFL);
        
        // Execute
        if (envp) {
            execve(path, argv, envp);
        } else {
            execv(path, argv);
        }
        
        _exit(127);
    }
    
    // Parent
    *master_fd = master;
    *child_pid = pid;
    return 0;
}

// Open a PTY master and get slave name
// Returns 0 on success, -1 on error
int pty_open(int *master_fd, char *slave_name, int slave_name_len, int width, int height) {
    int master = posix_openpt(O_RDWR | O_NOCTTY);
    if (master < 0) {
        return -1;
    }
    
    if (grantpt(master) < 0) {
        close(master);
        return -1;
    }
    
    if (unlockpt(master) < 0) {
        close(master);
        return -1;
    }
    
    char *name = ptsname(master);
    if (!name) {
        close(master);
        return -1;
    }
    
    strncpy(slave_name, name, slave_name_len - 1);
    slave_name[slave_name_len - 1] = '\0';
    
    // Set initial window size
    struct winsize ws = {
        .ws_row = height,
        .ws_col = width,
        .ws_xpixel = 0,
        .ws_ypixel = 0
    };
    ioctl(master, TIOCSWINSZ, &ws);
    
    // Copy termios from stdin to the PTY so it inherits the same settings
    if (isatty(STDIN_FILENO)) {
        struct termios tio;
        if (tcgetattr(STDIN_FILENO, &tio) == 0) {
            tcsetattr(master, TCSANOW, &tio);
        }
    }
    
    *master_fd = master;
    return 0;
}

// Spawn a child process with the PTY as its controlling terminal
// Returns 0 on success (in parent), -1 on error
int pty_spawn(const char *path, char *const argv[], char *const envp[], 
              const char *slave_name, const char *working_dir, int *child_pid) {
    pid_t pid = fork();
    
    if (pid < 0) {
        return -1;
    }
    
    if (pid == 0) {
        // Child process
        
        // Create a new session and become session leader
        // This detaches from the parent's controlling terminal
        if (setsid() < 0) {
            _exit(127);
        }
        
        // Open the slave PTY
        int slave = open(slave_name, O_RDWR);
        if (slave < 0) {
            _exit(127);
        }
        
        // Make the slave PTY our controlling terminal
        // TIOCSCTTY is required on Linux; on macOS, opening the PTY after setsid() is sufficient
#ifdef TIOCSCTTY
        if (ioctl(slave, TIOCSCTTY, 0) < 0) {
            // Not fatal on some systems
        }
#endif
        
        // Redirect stdin, stdout, stderr to the slave PTY
        dup2(slave, STDIN_FILENO);
        dup2(slave, STDOUT_FILENO);
        dup2(slave, STDERR_FILENO);
        
        // Close the original slave fd if it's not one of the standard fds
        if (slave > STDERR_FILENO) {
            close(slave);
        }
        
        // Change to the working directory
        if (working_dir && working_dir[0]) {
            if (chdir(working_dir) < 0) {
                // Not fatal, but log to stderr
                fprintf(stderr, "Warning: Could not change to directory %s\n", working_dir);
            }
        }
        
        // Reset signal handlers to defaults
        signal(SIGCHLD, SIG_DFL);
        signal(SIGHUP, SIG_DFL);
        signal(SIGINT, SIG_DFL);
        signal(SIGQUIT, SIG_DFL);
        signal(SIGTERM, SIG_DFL);
        signal(SIGALRM, SIG_DFL);
        
        // Execute the program
        if (envp) {
            execve(path, argv, envp);
        } else {
            execv(path, argv);
        }
        
        // If exec fails, exit with error
        _exit(127);
    }
    
    // Parent process
    *child_pid = pid;
    return 0;
}

// Wait for child with timeout (in milliseconds)
// Returns: 0 = child exited (status contains exit code)
//          1 = timeout (child still running)
//         -1 = error
int pty_wait(int pid, int timeout_ms, int *status) {
    int stat;
    int result;
    
    if (timeout_ms <= 0) {
        // Non-blocking check
        result = waitpid(pid, &stat, WNOHANG);
    } else {
        // Poll with short sleeps up to timeout
        int elapsed = 0;
        int interval = 10; // 10ms intervals
        
        while (elapsed < timeout_ms) {
            result = waitpid(pid, &stat, WNOHANG);
            if (result != 0) {
                break;
            }
            usleep(interval * 1000);
            elapsed += interval;
        }
    }
    
    if (result < 0) {
        return -1; // Error
    }
    
    if (result == 0) {
        return 1; // Timeout, child still running
    }
    
    // Child exited
    if (WIFEXITED(stat)) {
        *status = WEXITSTATUS(stat);
    } else if (WIFSIGNALED(stat)) {
        *status = 128 + WTERMSIG(stat);
    } else {
        *status = -1;
    }
    
    return 0;
}

// Resize the PTY
int pty_resize(int master_fd, int width, int height) {
    struct winsize ws = {
        .ws_row = height,
        .ws_col = width,
        .ws_xpixel = 0,
        .ws_ypixel = 0
    };
    return ioctl(master_fd, TIOCSWINSZ, &ws);
}
