#include <sys/ioctl.h>
#include <unistd.h>
#include <fcntl.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

int main(int argc, char** argv)
{
    const char* marker = getenv("STARTUP_MARKER");
    if (marker) {
        int fd = open(marker, O_WRONLY | O_CREAT | O_EXCL, 0600);
        if (fd >= 0) close(fd);
        fd = open("fallback-marker", O_WRONLY | O_CREAT | O_EXCL, 0600);
        if (fd >= 0) close(fd);
    }
    char cwd[4096];
    struct winsize size = {0};
    if (!getcwd(cwd, sizeof(cwd)) || ioctl(0, TIOCGWINSZ, &size) < 0) return 90;
    printf("cwd=%s\nargv0=%s\nargc=%d\narg=%s\ntoken=%s\nsize=%dx%d\ntty=%d\n",
        cwd, argv[0], argc, argc > 1 ? argv[1] : "",
        getenv("TOKEN") ? getenv("TOKEN") : "", size.ws_col, size.ws_row,
        isatty(0) && isatty(1) && isatty(2));
    const char* code = getenv("EXIT_CODE");
    return code ? atoi(code) : 0;
}
