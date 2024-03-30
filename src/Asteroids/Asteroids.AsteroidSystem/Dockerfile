# syntax=docker/dockerfile:1


FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS base 
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet tool install --global --no-cache dotnet-subset --version 0.3.2


FROM base as restore
WORKDIR /src
COPY . .
RUN dotnet subset restore Asteroids/Asteroids.AsteroidSystem/Asteroids.AsteroidSystem.csproj --root-directory /src --output /src/restored 


# Create a stage for building the application.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
COPY --from=restore /src/restored .
RUN dotnet restore Asteroids/Asteroids.AsteroidSystem/Asteroids.AsteroidSystem.csproj

COPY . /src
WORKDIR /src/Asteroids/Asteroids.AsteroidSystem

ARG TARGETARCH
RUN dotnet publish -a ${TARGETARCH/amd64/x64} --use-current-runtime -o /app


FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS development
COPY --from=restore /src/restored .
RUN dotnet restore Asteroids/Asteroids.AsteroidSystem/Asteroids.AsteroidSystem.csproj 

COPY . /src
WORKDIR /src/Asteroids/Asteroids.AsteroidSystem
# dotnet watch by default avoids restore
CMD dotnet watch run --no-launch-profile --non-interactive


FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

# Copy everything needed to run the app from the "build" stage.
COPY --from=build /app .

# Switch to a non-privileged user (defined in the base image) that the app will run under.
# See https://docs.docker.com/go/dockerfile-user-best-practices/
# and https://github.com/dotnet/dotnet-docker/discussions/4764
USER $APP_UID

ENTRYPOINT ["dotnet", "Asteroids.AsteroidSystem.dll"]
