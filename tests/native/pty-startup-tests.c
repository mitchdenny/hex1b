#define _GNU_SOURCE
#include <sys/types.h>
#include <sys/wait.h>
#include <sys/ioctl.h>
#include <sys/stat.h>
#include <unistd.h>
#include <fcntl.h>
#include <signal.h>
#include <errno.h>
#include <string.h>
#include <stdlib.h>
#include <stdio.h>
#include <termios.h>
#include <stdint.h>
#include <poll.h>
#include <pthread.h>
#include <time.h>
#include <stdarg.h>
#include <limits.h>
#ifdef __APPLE__
#include <util.h>
#else
#include <pty.h>
#endif

static int fail_pipe, fail_fork, fail_fcntl, interrupt_read, interrupt_write;
static int interrupt_wait, clobber_close, read_chunk, write_chunk;
static int barrier_notice = -1, barrier_release = -1, early_exit;
static int interrupt_poll, fail_read, fail_close, expired_clock, clock_calls;
static int kill_calls;
#ifdef __APPLE__
static int test_pipe(int* fds);
#else
static int test_pipe2(int* fds, int flags);
#endif
static int test_fcntl(int fd, int command, ...);
static pid_t test_forkpty(int* fd, char* name, const struct termios* attributes,
    const struct winsize* size);
static int test_chdir(const char* path);
static ssize_t test_read(int fd, void* buffer, size_t count);
static ssize_t test_write(int fd, const void* buffer, size_t count);
static pid_t test_waitpid(pid_t pid, int* status, int flags);
static int test_close(int fd);
static int test_poll(struct pollfd* descriptors, nfds_t count, int timeout);
static int test_clock_gettime(clockid_t clock_id, struct timespec* value);
static int test_kill(pid_t pid, int sig);

/* Faults and barriers exist only in this translation unit, never in the dylib/so. */
#define pipe test_pipe
#define pipe2 test_pipe2
#define fcntl test_fcntl
#define forkpty test_forkpty
#define chdir test_chdir
#define read test_read
#define write test_write
#define waitpid test_waitpid
#define close test_close
#define poll test_poll
#define clock_gettime test_clock_gettime
#define kill test_kill
#include "../../src/Hex1b/native/hex1binterop.c"
#undef pipe
#undef pipe2
#undef fcntl
#undef forkpty
#undef chdir
#undef read
#undef write
#undef waitpid
#undef close
#undef poll
#undef clock_gettime
#undef kill

#ifdef __APPLE__
static int test_pipe(int* fds)
{
    if (fail_pipe) { errno = EMFILE; return -1; }
    return pipe(fds);
}
#else
static int test_pipe2(int* fds, int flags)
{
    if (fail_pipe) { errno = EMFILE; return -1; }
    return pipe2(fds, flags);
}
#endif
static int test_fcntl(int fd, int command, ...)
{
    va_list args;
    va_start(args, command);
    int argument = va_arg(args, int);
    va_end(args);
    if (fail_fcntl && --fail_fcntl == 0) { errno = EIO; return -1; }
    return fcntl(fd, command, argument);
}
static pid_t test_forkpty(int* fd, char* name, const struct termios* attributes,
    const struct winsize* size)
{
    if (fail_fork) { errno = EAGAIN; return -1; }
    pid_t pid = forkpty(fd, name, (struct termios*)attributes, (struct winsize*)size);
    if (pid == 0 && early_exit) _exit(1);
    return pid;
}
static int test_chdir(const char* path)
{
    if (barrier_notice >= 0) {
        char byte = 'x';
        if (write(barrier_notice, &byte, 1) != 1) _exit(91);
        ssize_t result;
        do { result = read(barrier_release, &byte, 1); } while (result < 0 && errno == EINTR);
        if (result != 1) _exit(92);
        close(barrier_notice);
        close(barrier_release);
    }
    return chdir(path);
}
static ssize_t test_read(int fd, void* buffer, size_t count)
{
    if (fail_read) { errno = EIO; return -1; }
    if (interrupt_read) { interrupt_read = 0; errno = EINTR; return -1; }
    if (read_chunk && count > (size_t)read_chunk) count = (size_t)read_chunk;
    return read(fd, buffer, count);
}
static ssize_t test_write(int fd, const void* buffer, size_t count)
{
    if (interrupt_write) { interrupt_write = 0; errno = EINTR; return -1; }
    if (write_chunk && count > (size_t)write_chunk) count = (size_t)write_chunk;
    return write(fd, buffer, count);
}
static pid_t test_waitpid(pid_t pid, int* status, int flags)
{
    if (interrupt_wait) { interrupt_wait = 0; errno = EINTR; return -1; }
    return waitpid(pid, status, flags);
}
static int test_close(int fd)
{
    int result = close(fd);
    if (fail_close) { fail_close = 0; errno = EIO; return -1; }
    if (clobber_close) errno = EBADF;
    return result;
}
static int test_poll(struct pollfd* descriptors, nfds_t count, int timeout)
{
    if (interrupt_poll) { interrupt_poll = 0; errno = EINTR; return -1; }
    return poll(descriptors, count, timeout);
}
static int test_clock_gettime(clockid_t clock_id, struct timespec* value)
{
    int result = clock_gettime(clock_id, value);
    if (expired_clock && clock_calls++ > 0) value->tv_sec += 11;
    return result;
}
static int test_kill(pid_t pid, int sig)
{
    ++kill_calls;
    return kill(pid, sig);
}

static volatile sig_atomic_t children[128];
static int checks;
static char fixture[PATH_MAX], target[PATH_MAX], marker[PATH_MAX];
static int fixture_created;
static const char* environment[] = { "TOKEN=isolated value \xc3\xa9", NULL };

static void stop_children(int sig)
{
    for (int i = 0; i < 128; ++i) {
        if (children[i] > 0) {
            kill(children[i], SIGKILL);
            while (waitpid(children[i], NULL, 0) < 0 && errno == EINTR) {}
        }
    }
    if (sig) {
        const char message[] = "native startup test watchdog expired\n";
        write(2, message, sizeof(message) - 1);
        _exit(124);
    }
}

static void fail(const char* expression, int line)
{
    fprintf(stderr, "FAIL line %d: %s (errno=%d)\n", line, expression, errno);
    stop_children(0);
    if (fixture_created && chdir(fixture) == 0) {
        chmod("denied", 0700);
        rmdir("denied");
        rmdir("spaces \xc3\xa9");
        rmdir("race");
        unlink("file");
        unlink("fallback-marker");
        unlink("target-marker");
        chdir("..");
        rmdir(fixture);
    }
    exit(1);
}
#define CHECK(condition) do { ++checks; if (!(condition)) fail(#condition, __LINE__); } while (0)

static void track(int pid)
{
    for (int i = 0; i < 128; ++i) if (!children[i]) { children[i] = pid; return; }
    fail("child tracking capacity", __LINE__);
}
static void untrack(int pid)
{
    for (int i = 0; i < 128; ++i) if (children[i] == pid) children[i] = 0;
}
static int begin(int shell, const char* path, const char* cwd, const char** env,
    int* master, int* pid, int* startup, int* stage)
{
    const char* argv[] = { "literal argv zero", "argument with spaces \xc3\xa9", NULL };
    int result = shell ?
        hex1b_forkpty_shell_env_start(path, cwd, env, 93, 37, master, pid, startup, stage) :
        hex1b_forkpty_exec_env_start(path, argv, 2, cwd, env, 93, 37, master, pid, startup, stage);
    if (result == 0) track(*pid);
    return result;
}
static int complete(int startup, struct hex1b_startup_state* state, int* stage)
{
    for (int i = 0; i < 100; ++i) {
        int result = hex1b_poll_startup(startup, 50, state, stage);
        if (result != 1) return result;
    }
    fail("startup completed within five seconds", __LINE__);
    return -1;
}
static void abort_and_check(int master, int pid, int startup)
{
    interrupt_wait = 1;
    CHECK(hex1b_abort_startup(master, pid, startup) == 0);
    untrack(pid);
    CHECK(fcntl(master, F_GETFD) == -1 && errno == EBADF);
    CHECK(fcntl(startup, F_GETFD) == -1 && errno == EBADF);
    CHECK(waitpid(pid, NULL, WNOHANG) == -1 && errno == ECHILD);
}
static void failure_case(int shell, const char* path, const char* cwd, int expected, int expected_stage)
{
    char marker_env[PATH_MAX + 32];
    snprintf(marker_env, sizeof(marker_env), "STARTUP_MARKER=%s", marker);
    const char* env[] = { marker_env, NULL };
    int master, pid, startup, stage;
    struct hex1b_startup_state state = {0};
    CHECK(begin(shell, path, cwd, env, &master, &pid, &startup, &stage) == 0);
    CHECK(complete(startup, &state, &stage) == -1);
    CHECK(errno == expected && stage == expected_stage);
    abort_and_check(master, pid, startup);
    CHECK(access(marker, F_OK) == -1 && errno == ENOENT);
    CHECK(access("fallback-marker", F_OK) == -1 && errno == ENOENT);
}
static void success_case(int shell, const char* cwd, const char* expected_cwd, int exit_code)
{
    char exit_env[32];
    snprintf(exit_env, sizeof(exit_env), "EXIT_CODE=%d", exit_code);
    const char* env[] = { environment[0], exit_env, NULL };
    int master, pid, startup, stage;
    struct hex1b_startup_state state = {0};
    CHECK(begin(shell, target, cwd, env, &master, &pid, &startup, &stage) == 0);
    CHECK(master > 2 && startup > 2 && pid > 0);
    CHECK(fcntl(master, F_GETFD) & FD_CLOEXEC);
    CHECK(fcntl(startup, F_GETFD) & FD_CLOEXEC);
    CHECK(complete(startup, &state, &stage) == 0 && state.ready == 1);
    close(startup);
    char output[8192] = {0};
    size_t used = 0;
    for (;;) {
        struct pollfd fd = { .fd = master, .events = POLLIN };
        CHECK(poll(&fd, 1, 5000) > 0);
        ssize_t count = read(master, output + used, sizeof(output) - used - 1);
        if (count == 0 || (count < 0 && errno == EIO)) break;
        CHECK(count > 0);
        used += (size_t)count;
        CHECK(used < sizeof(output) - 1);
    }
    int status;
    CHECK(waitpid(pid, &status, 0) == pid);
    untrack(pid);
    close(master);
    CHECK(WIFEXITED(status) && WEXITSTATUS(status) == exit_code);
    CHECK(strstr(output, expected_cwd) != NULL);
    CHECK(strstr(output, "token=isolated value \xc3\xa9") != NULL);
    CHECK(strstr(output, "size=93x37") != NULL && strstr(output, "tty=1") != NULL);
    CHECK(strstr(output, shell ? "argv0=-startup-target" : "argv0=literal argv zero") != NULL);
    if (!shell) CHECK(strstr(output, "arg=argument with spaces \xc3\xa9") != NULL);
}

static void protocol_cases(void)
{
    const int32_t cases[][4] = {
        {1, 0, 0, 0}, {1, 0, 3, ENOENT}, {2, EACCES, 0, 0},
        {99, 0, 0, 0}, {1, 1, 0, 0}, {3, ENOENT, 0, 0},
        {1, 0, 1, 0}, {1, 0, 2, ENOENT}, {2, 0, 0, 0}
    };
    const int lengths[] = {8, 16, 8, 8, 8, 8, 16, 16, 8, 0, 3, 9};
    for (int i = 0; i < 12; ++i) {
        int pipefd[2], stage;
        CHECK(pipe(pipefd) == 0);
        CHECK(fcntl(pipefd[0], F_SETFL, O_NONBLOCK) == 0);
        const void* data = cases[i < 9 ? i : 0];
        CHECK(write(pipefd[1], data, lengths[i]) == lengths[i]);
        close(pipefd[1]);
        struct hex1b_startup_state state = {0};
        read_chunk = 1;
        interrupt_read = 1;
        int result = complete(pipefd[0], &state, &stage);
        read_chunk = 0;
        CHECK(result == (i == 0 ? 0 : -1));
        if (i) CHECK(errno == (i == 1 ? ENOENT : i == 2 ? EACCES : EPROTO));
        if (i) CHECK(stage == (i == 1 ? 3 : i == 2 ? 2 : 4));
        close(pipefd[0]);
    }
    int pipefd[2], stage;
    CHECK(pipe(pipefd) == 0);
    CHECK(fcntl(pipefd[0], F_SETFL, O_NONBLOCK) == 0);
    struct hex1b_startup_state state = {0};
    int32_t ready[2] = {1, 0};
    interrupt_poll = 1;
    CHECK(hex1b_poll_startup(pipefd[0], 0, &state, &stage) == 1);
    CHECK(hex1b_poll_startup(pipefd[0], 0, &state, &stage) == 1);
    CHECK(write(pipefd[1], ready, 3) == 3);
    fail_read = 1;
    CHECK(hex1b_poll_startup(pipefd[0], 0, &state, &stage) == -1 && errno == EIO && stage == 4);
    fail_read = 0;
    CHECK(hex1b_poll_startup(pipefd[0], 0, &state, &stage) == 1 && state.bytes_read == 3);
    CHECK(write(pipefd[1], (char*)ready + 3, 5) == 5);
    CHECK(hex1b_poll_startup(pipefd[0], 0, &state, &stage) == 1 && state.ready);
    close(pipefd[1]);
    CHECK(complete(pipefd[0], &state, &stage) == 0);
    close(pipefd[0]);
    CHECK(hex1b_poll_startup(pipefd[0], 0, &state, &stage) == -1 && errno == EBADF);
}

static void barrier_cases(void)
{
    for (int shell = 0; shell < 2; ++shell) {
        CHECK(mkdir("race", 0700) == 0);
        int notice[2], release[2];
        CHECK(pipe(notice) == 0 && pipe(release) == 0);
        barrier_notice = notice[1]; barrier_release = release[0];
        int master, pid, startup, stage;
        CHECK(begin(shell, target, "race", environment, &master, &pid, &startup, &stage) == 0);
        barrier_notice = barrier_release = -1;
        close(notice[1]); close(release[0]);
        struct pollfd fd = { .fd = notice[0], .events = POLLIN };
        CHECK(poll(&fd, 1, 5000) == 1);
        char byte;
        CHECK(read(notice[0], &byte, 1) == 1);
        struct hex1b_startup_state state = {0};
        CHECK(hex1b_poll_startup(startup, 0, &state, &stage) == 1);
        /* A stalled child must not hold the spawn mutex or another launch's writer. */
        success_case(!shell, NULL, fixture, 0);
        CHECK(rmdir("race") == 0);
        CHECK(write(release[1], "x", 1) == 1);
        CHECK(complete(startup, &state, &stage) == -1 && errno == ENOENT && stage == 2);
        abort_and_check(master, pid, startup);
        close(notice[0]); close(release[1]);
    }
}

static void setup_cases(void)
{
    for (int shell = 0; shell < 2; ++shell) {
        int master = 99, pid = 99, startup = 99, stage;
        CHECK(begin(shell, NULL, fixture, environment, &master, &pid, &startup, &stage) == -1);
        CHECK(errno == EINVAL && master == -1 && pid == -1 && startup == -1 && stage == 1);
        CHECK(begin(shell, target, fixture, NULL, &master, &pid, &startup, &stage) == -1);
        CHECK(errno == EINVAL && master == -1 && pid == -1 && startup == -1 && stage == 1);
        for (int fault = 0; fault < 6; ++fault) {
            fail_pipe = fault == 0;
            fail_fork = fault == 1;
            fail_fcntl = fault >= 2 ? fault - 1 : 0;
            clobber_close = 1;
            int master = 99, pid = 99, startup = 99, stage;
            CHECK(begin(shell, target, fixture, environment, &master, &pid, &startup, &stage) == -1);
            CHECK(errno == (fault == 0 ? EMFILE : fault == 1 ? EAGAIN : EIO));
            CHECK(master == -1 && pid == -1 && startup == -1 && stage == 1);
            fail_pipe = fail_fork = fail_fcntl = clobber_close = 0;
        }
        early_exit = 1;
        failure_case(shell, target, fixture, EPROTO, 4);
        early_exit = 0;
    }
}

static void legacy_cases(void)
{
    for (int variant = 0; variant < 4; ++variant) {
        const char* argv[] = {target, NULL};
        int master = 99, pid = 99, result;
        clobber_close = 1;
        switch (variant) {
        case 0: result = hex1b_forkpty_shell(target, "missing", 80, 24, &master, &pid); break;
        case 1: result = hex1b_forkpty_shell_env(target, "missing", environment, 80, 24, &master, &pid); break;
        case 2: result = hex1b_forkpty_exec(target, argv, 1, "missing", 80, 24, &master, &pid); break;
        default: result = hex1b_forkpty_exec_env(target, argv, 1, "missing", environment, 80, 24, &master, &pid); break;
        }
        CHECK(result == -1 && errno == ENOENT && master == -1 && pid == -1);
        clobber_close = 0;
        switch (variant) {
        case 0: result = hex1b_forkpty_shell(target, fixture, 80, 24, &master, &pid); break;
        case 1: result = hex1b_forkpty_shell_env(target, fixture, environment, 80, 24, &master, &pid); break;
        case 2: result = hex1b_forkpty_exec(target, argv, 1, fixture, 80, 24, &master, &pid); break;
        default: result = hex1b_forkpty_exec_env(target, argv, 1, fixture, environment, 80, 24, &master, &pid); break;
        }
        CHECK(result == 0 && master > 2 && pid > 0);
        track(pid);
        CHECK(hex1b_abort_startup(master, pid, -1) == 0);
        untrack(pid);
    }
    int notice[2], release[2], master = 99, pid = 99;
    CHECK(pipe(notice) == 0 && pipe(release) == 0);
    barrier_notice = notice[1]; barrier_release = release[0];
    expired_clock = 1; clock_calls = 0; clobber_close = 1;
    CHECK(hex1b_forkpty_shell_env(target, fixture, environment, 80, 24, &master, &pid) == -1);
    CHECK(errno == ETIMEDOUT && master == -1 && pid == -1);
    expired_clock = clobber_close = 0;
    barrier_notice = barrier_release = -1;
    close(notice[0]); close(notice[1]); close(release[0]); close(release[1]);
    CHECK(waitpid(-1, NULL, WNOHANG) == -1 && errno == ECHILD);
}

struct concurrent_launch {
    int shell, result, master, pid, startup, stage;
    const char* cwd;
};
static void* launch_thread(void* argument)
{
    struct concurrent_launch* launch = argument;
    const char* argv[] = { "parallel target", NULL };
    launch->result = launch->shell ?
        hex1b_forkpty_shell_env_start(target, launch->cwd, environment, 80, 24,
            &launch->master, &launch->pid, &launch->startup, &launch->stage) :
        hex1b_forkpty_exec_env_start(target, argv, 1, launch->cwd, environment, 80, 24,
            &launch->master, &launch->pid, &launch->startup, &launch->stage);
    return NULL;
}
static void concurrent_cases(void)
{
    pthread_t threads[8];
    struct concurrent_launch launches[8];
    for (int i = 0; i < 8; ++i) {
        launches[i] = (struct concurrent_launch) { .shell = i % 2,
            .cwd = i < 4 ? fixture : "missing" };
        CHECK(pthread_create(&threads[i], NULL, launch_thread, &launches[i]) == 0);
    }
    for (int i = 0; i < 8; ++i) {
        CHECK(pthread_join(threads[i], NULL) == 0);
    }
    for (int i = 0; i < 8; ++i) {
        struct concurrent_launch* launch = &launches[i];
        CHECK(launch->result == 0);
        track(launch->pid);
        struct hex1b_startup_state state = {0};
        int result = complete(launch->startup, &state, &launch->stage);
        CHECK(i < 4 ? result == 0 : result == -1 && errno == ENOENT && launch->stage == 2);
        abort_and_check(launch->master, launch->pid, launch->startup);
    }
}

static void abort_pending_case(void)
{
    int notice[2], release[2];
    CHECK(pipe(notice) == 0 && pipe(release) == 0);
    barrier_notice = notice[1]; barrier_release = release[0];
    int master, pid, startup, stage;
    CHECK(begin(1, target, fixture, environment, &master, &pid, &startup, &stage) == 0);
    barrier_notice = barrier_release = -1;
    struct pollfd fd = { .fd = notice[0], .events = POLLIN };
    CHECK(poll(&fd, 1, 5000) == 1);
    struct hex1b_startup_state state = {0};
    CHECK(hex1b_poll_startup(startup, 1, &state, &stage) == 1 && state.ready == 0);
    fail_close = 1;
    interrupt_wait = 1;
    CHECK(hex1b_abort_startup(master, pid, startup) == -1 && errno == EIO);
    untrack(pid);
    CHECK(waitpid(pid, NULL, WNOHANG) == -1 && errno == ECHILD);
    CHECK(fcntl(master, F_GETFD) == -1 && errno == EBADF);
    CHECK(fcntl(startup, F_GETFD) == -1 && errno == EBADF);
    close(notice[0]); close(notice[1]); close(release[0]); close(release[1]);
}

static void abort_reaped_case(void)
{
    int master, pid, startup, stage;
    struct hex1b_startup_state state = {0};
    CHECK(begin(0, target, fixture, environment, &master, &pid, &startup, &stage) == 0);
    CHECK(complete(startup, &state, &stage) == 0);
    CHECK(waitpid(pid, NULL, 0) == pid);
    untrack(pid);
    int before = kill_calls;
    CHECK(hex1b_abort_startup(master, pid, startup) == 0);
    CHECK(kill_calls == before);
    CHECK(fcntl(master, F_GETFD) == -1 && errno == EBADF);
    CHECK(fcntl(startup, F_GETFD) == -1 && errno == EBADF);
}

static int descriptor_count(void)
{
    int count = 0;
    for (int fd = 0; fd < 4096; ++fd) if (fcntl(fd, F_GETFD) >= 0) ++count;
    return count;
}

static void closed_stdio_case(void)
{
    pid_t tester = fork();
    CHECK(tester >= 0);
    if (tester == 0) {
        close(0); close(1); close(2);
        int master, pid, startup, stage;
        struct hex1b_startup_state state = {0};
        if (begin(1, target, fixture, environment, &master, &pid, &startup, &stage) != 0) _exit(93);
        if (master <= 2 || startup <= 2 || complete(startup, &state, &stage) != 0) _exit(94);
        if (hex1b_abort_startup(master, pid, startup) != 0) _exit(95);
        _exit(0);
    }
    track(tester);
    int status;
    CHECK(waitpid(tester, &status, 0) == tester && WIFEXITED(status) && WEXITSTATUS(status) == 0);
    untrack(tester);
}

int main(int argc, char** argv)
{
    CHECK(argc == 2 && realpath(argv[1], target) != NULL);
    signal(SIGALRM, stop_children);
    alarm(60);
    char original[PATH_MAX];
    CHECK(getcwd(original, sizeof(original)) != NULL);
    char relative[64];
    snprintf(relative, sizeof(relative), "../obj/native/startup-fixture-%ld", (long)getpid());
    CHECK(mkdir(relative, 0700) == 0);
    CHECK(realpath(relative, fixture) != NULL);
    fixture_created = 1;
    CHECK(chdir(fixture) == 0);
    CHECK(snprintf(marker, sizeof(marker), "%s/target-marker", fixture) < (int)sizeof(marker));
    CHECK(mkdir("spaces \xc3\xa9", 0700) == 0);
    CHECK(mkdir("denied", 0600) == 0);
    int file = open("file", O_CREAT | O_EXCL | O_WRONLY, 0600);
    CHECK(file >= 0); close(file);
    int baseline = descriptor_count();
    protocol_cases();
    setup_cases();
    for (int shell = 0; shell < 2; ++shell) {
        failure_case(shell, target, "missing", ENOENT, 2);
        failure_case(shell, target, "file", ENOTDIR, 2);
        if (geteuid() != 0 && access("denied", X_OK) != 0)
            failure_case(shell, target, "denied", EACCES, 2);
        else puts("SKIP no-search cwd: effective user can traverse fixture");
        char nonexec[PATH_MAX];
        CHECK(snprintf(nonexec, sizeof(nonexec), "%s/missing", fixture) < (int)sizeof(nonexec));
        failure_case(shell, nonexec, fixture, ENOENT, 3);
        CHECK(snprintf(nonexec, sizeof(nonexec), "%s/file", fixture) < (int)sizeof(nonexec));
        failure_case(shell, nonexec, fixture, EACCES, 3);
        interrupt_write = 1; write_chunk = 1;
        success_case(shell, NULL, fixture, 0);
        interrupt_write = write_chunk = 0;
        success_case(shell, "", fixture, 1);
        success_case(shell, fixture, fixture, 125);
        char unicode[PATH_MAX];
        CHECK(snprintf(unicode, sizeof(unicode), "%s/spaces \xc3\xa9", fixture) < (int)sizeof(unicode));
        success_case(shell, "spaces \xc3\xa9", unicode, 127);
        for (int i = 0; i < 20; ++i) failure_case(shell, target, "missing", ENOENT, 2);
    }
    barrier_cases();
    legacy_cases();
    concurrent_cases();
    abort_pending_case();
    abort_reaped_case();
    closed_stdio_case();
    CHECK(descriptor_count() == baseline);
    CHECK(waitpid(-1, NULL, WNOHANG) == -1 && errno == ECHILD);
    CHECK(chmod("denied", 0700) == 0);
    CHECK(rmdir("denied") == 0 && rmdir("spaces \xc3\xa9") == 0 && unlink("file") == 0);
    CHECK(chdir(original) == 0 && rmdir(relative) == 0);
    alarm(0);
    printf("PASS native startup: %d assertions\n", checks);
    return 0;
}
