# Makefile for libhex1binterop - Native interop library for Hex1b
#
# Builds for the current platform:
#   make           - Build for current platform
#   make clean     - Remove build artifacts
#
# Cross-compilation is handled separately via CI/CD

CC = gcc
CFLAGS = -Wall -Wextra -O2 -fPIC
LDFLAGS = -shared

# Detect platform and set library extension and flags
UNAME_S := $(shell uname -s)
UNAME_M := $(shell uname -m)

ifeq ($(UNAME_S),Darwin)
    TARGET = libhex1binterop.dylib
    LDFLAGS += -dynamiclib -install_name @rpath/libhex1binterop.dylib
    # macOS needs util library for PTY functions
    LDFLAGS += -lutil
else
    TARGET = libhex1binterop.so
    LDFLAGS += -lpthread -lutil
endif

# Determine RID for output directory
ifeq ($(UNAME_S),Darwin)
    ifeq ($(UNAME_M),arm64)
        RID = osx-arm64
    else
        RID = osx-x64
    endif
else
    ifeq ($(UNAME_M),aarch64)
        RID = linux-arm64
    else
        RID = linux-x64
    endif
endif

SOURCES = hex1binterop.c
OBJECTS = $(SOURCES:.c=.o)

# Output to runtimes directory structure for NuGet packaging
OUTDIR = ../runtimes/$(RID)/native

all: $(OUTDIR)/$(TARGET)

$(OUTDIR)/$(TARGET): $(OBJECTS)
	@mkdir -p $(OUTDIR)
	$(CC) $(LDFLAGS) -o $@ $^

%.o: %.c
	$(CC) $(CFLAGS) -c $< -o $@

clean:
	rm -f $(OBJECTS)
	rm -rf ../runtimes

# Install to system (for development/testing)
install: $(OUTDIR)/$(TARGET)
	@echo "Native library built at $(OUTDIR)/$(TARGET)"
	@echo "For development, you can copy to /usr/local/lib:"
	@echo "  sudo cp $(OUTDIR)/$(TARGET) /usr/local/lib/"

.PHONY: all clean install
