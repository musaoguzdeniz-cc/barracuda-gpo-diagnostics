# Barracuda Grup İlkesi Teşhis Aracı

[Windows EXE'yi indir](https://github.com/musaoguzdeniz-cc/barracuda-gpo-diagnostics/releases/latest/download/BarracudaGpoDiagnostics.exe)

Windows x64 için RSoP rapor okuyucusu. **Canlı sonucu oku** yerel `gpresult /x` çıktısını açar. Başka bir makinede oluşturulan XML, **XML rapor aç** ile incelenebilir.

```cmd
gpresult /x "%TEMP%\gpo-raporu.xml" /f
```

Bilgisayar ve kullanıcı kapsamları ayrıdır. Ayarlar ve GPO listesi aranabilir. Microsoft Store, Windows Update ve proxy için hızlı konu filtreleri vardır. Ayrıntı paneli XML’de yer alan değeri ve kaynak GPO’yu gösterir. Eksik bir kapsam veya alan için görünüm, raporun sağlayabildiği bilgiyi gösterir. Bilgisayar kapsamı için yönetici yetkisi gerekebilir. Uygulama ilke ve kayıt defteri ayarlarını değiştirmez.

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```
