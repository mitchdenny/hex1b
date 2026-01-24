# Base image for .NET 10.0 development
FROM mcr.microsoft.com/devcontainers/dotnet:10.0-noble

# Workaround for Yarn APT repo GPG key expiration issue.
# The base image includes Yarn apt sources which have an expired GPG key,
# causing apt-get update to fail during devcontainer feature installation.
# Since this project doesn't need Yarn, we remove the problematic sources.
# See: https://github.com/devcontainers/features/issues/1543
USER root
RUN rm -f /etc/apt/sources.list.d/yarn.list \
    && rm -f /usr/share/keyrings/yarn.gpg \
    && apt-get update
