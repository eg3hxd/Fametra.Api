FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /App

# Bütün dosyaları kopyala
COPY . ./

# Paketleri yükle
RUN dotnet restore

# Release (Canlı) versiyonunu derle
RUN dotnet publish -c Release -o out

# Çalışma zamanı imajı (Sadece çalışması için gereken hafif sürüm)
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /App
COPY --from=build-env /App/out .

# Render ve diğer sunucular genellikle PORT çevre değişkenini okur, ancak biz 5261'i de açıyoruz.
ENV ASPNETCORE_URLS=http://+:5261
EXPOSE 5261

ENTRYPOINT ["dotnet", "Fametra.Api.dll"]
