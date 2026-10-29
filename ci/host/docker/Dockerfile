FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS runtime

WORKDIR /app

COPY ./ ./

ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "Coop.Services.Surface.dll"]
