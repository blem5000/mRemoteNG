# mRemoteNG — build Blem5000

Zarządzanie wieloma połączeniami zdalnymi w jednym oknie: RDP, SSH, VNC, Telnet, HTTP/HTTPS
i dowolne narzędzia zewnętrzne. Ten projekt jest kontynuacją mRemoteNG (GPLv2) prowadzoną przez
[Blem5000](https://github.com/blem5000) — z poprawkami błędów, ciemnym motywem, własnym
aktualizatorem i jednym wydaniem zawierającym obie paczki.

## Pobieranie

Każde wydanie jest publikowane jako **jeden release z dwoma plikami** — wybierz jeden:

| Plik | .NET | Rozmiar | Kiedy wybrać |
| --- | --- | --- | --- |
| `mRemoteNG-1.78.2-fix-YYYYMMDD-x64.zip` | wymaga zainstalowanego .NET 11 Desktop Runtime | ~58 MB | masz już .NET i chcesz mniejszą paczkę |
| `mRemoteNG-1.78.2-fix-20261116e-x64-selfcontained.zip` | .NET 11 w paczce | ~90 MB | nie chcesz instalować .NET |

Najnowsze wydanie: <https://github.com/blem5000/mRemoteNG/releases/latest>

Instalacja: rozpakuj zip do wybranego katalogu i uruchom `mRemoteNG.exe`. Nic więcej nie trzeba —
program nie wymaga instalatora ani uprawnień administratora.

## Co jest inne w tym buildzie

### Poprawki błędów
- **Zamykanie paneli i zakładek** — brak crashy `ObjectDisposedException` / `InvalidCastException`
  przy zamykaniu panelu z wieloma połączeniami oraz zakładki, która jeszcze się łączy.
- **Zamykanie programu** — jedno pytanie o zamknięcie połączeń, a odpowiedź „Nie” naprawdę
  zostawia wszystko otwarte.
- **Okno Opcji** — strony nie rzucają wyjątków przy braku ustawień z rejestru, lista stron jest
  kompletna od pierwszego otwarcia, „Anuluj” cofa zmiany.
- **CSV / XML** — eksport i import nie gubią już ustawień (`Color`, `TabColor`,
  `ConnectionFrameColor`, `RDGatewayAccessToken`, flagi dziedziczenia); plik XML przechodzi walidację
  względem schematu.
- **Narzędzia zewnętrzne** — argumenty z hasłami zawierającymi spacje, `=` i znaki specjalne są
  przekazywane poprawnie, również przy „Uruchom jako administrator”.
- **Bezpieczeństwo zależności** — SSH.NET 2026.0.1, BouncyCastle 2.7.0, log4net 3.6.0;
  brak podatnych pakietów.
- Dołączony `PuTTYNG.exe` w wersji **0.85** (mRemoteNG, without embedded help) z poprawkami
  bezpieczeństwa z 0.84 i 0.85.

### Ciemny motyw
- Motyw wybrany w Opcjach jest zapamiętywany i przywracany po restarcie.
- Ciemny pasek tytułu okien (Windows 10/11), spójne kolory okna Opcji, zakładek i okna UltraVNC.
- Po zmianie motywu program proponuje **restart od razu**, żeby zmiana była widoczna bez czekania.

### Ustawienia, które przeżywają przenoszenie folderu
Program trzyma `user.config` w kolejności:

1. `user.config` **w folderze programu** — jeśli tam jest, jest używany (tryb przenośny),
2. w przeciwnym razie standardowa kopia w `%APPDATA%\mRemoteNG Connection Manager\user.config`,
   która jest odtwarzana przy starcie.

Dzięki temu przeniesienie katalogu z programem albo podmiana paczki na nowszą wersję nie kasuje
ustawień, layoutu paneli ani wpisów połączeń.

### Aktualizacje z tego repozytorium
Program sprawdza wydania na GitHubie (`blem5000/mRemoteNG`) i pobiera paczkę dopasowaną do
uruchomionej wersji:

- uruchomiona wersja self-contained (ma `coreclr.dll` w katalogu programu) → `*-x64-selfcontained.zip`,
- pozostałe → `*-x64.zip`.

Aktualizacja jest rozpakowywana do katalogu tymczasowego, a następnie mały skrypt PowerShell
czeka na zamknięcie programu, podmienia pliki i uruchamia go ponownie. Ustawienia użytkownika
(`user.config`, konfiguracja połączeń, layout) nie są przy tym ruszane.

Sprawdzić można ręcznie: **Pomoc → Sprawdź aktualizacje**, albo automatycznie przy starcie
(Opcje → Aktualizacje, domyślnie raz w tygodniu).

> Uwaga: GitHub udostępnia API bez logowania z limitem 60 zapytań na godzinę na adres IP —
> przy normalnym użytkowaniu to w zupełności wystarcza.

### Skalowanie interfejsu (DPI)

Jeśli interfejs wydaje Ci się zbyt duży lub zbyt mały (menu, drzewo połączeń, ikony), zmień tryb w
**Opcje → Wygląd → Interface scaling**. Dostępne wartości:

- `DpiUnaware` — Windows skaluje okno jako bitmapę; daje najmniejszy interfejs (podobny do wersji .NET 9),
- `SystemAware` — skalowanie raz przy starcie; interfejs większy,
- `PerMonitorV2` — domyślna, skalowanie niezależnie dla każdego monitora; interfejs największy.

Zmiana wymaga restartu programu. Tryb zapisywany jest razem z pozostałymi ustawieniami.
Dla celów testowych można go nadpisać zmienną środowiskową `MREMOTENG_DPI_MODE` bez zmiany
zapisanej opcji.

## Wymagania

- Windows 10 (1809+) / 11 x64 lub ARM64,
- .NET 11 Desktop Runtime (albo paczka self-contained, która ma runtime w środku),
- Visual Studio 2026 (18.12) i SDK 11.0.100 do budowania ze źródeł,
- Visual C++ Redistributable x64 (program sam zaproponuje pobranie, jeśli go brakuje).

## Budowanie ze źródeł

Wymagany jest **Visual Studio 2026 (MSBuild 18.12+)** i **SDK 11.0.100**. `dotnet build` nie
wystarczy — projekt używa referencji COM, które obsługuje tylko MSBuild z Visual Studio.

```powershell
# build framework-dependent
MSBuild.exe mRemoteNG.sln -p:Configuration=Release -p:Platform=x64

# build self-contained (wynik w artifacts\sc-x64)
MSBuild.exe mRemoteNG\mRemoteNG.csproj -t:Publish -p:Configuration=Release -p:Platform=x64 `
    -p:RuntimeIdentifier=win-x64 -p:SelfContained=true -p:PublishDir=artifacts\sc-x64\
```

Testy:

```powershell
MSBuild.exe mRemoteNGTests\mRemoteNGTests.csproj -p:Configuration=Debug -p:Platform=x64
vstest.console.exe mRemoteNGTests\bin\x64\Debug\mRemoteNGTests.dll
```

### Wydawanie nowej wersji

1. Podbij wersję/tag wydania.
2. Zaktualizuj `ForkUpdateChannel.BuildTag` (`mRemoteNG/App/Checks/ForkUpdateChannel.cs`) na nowy
   tag — po tym program rozpoznaje, że jest dostępna nowsza kompilacja.
3. Opublikuj **jeden** release z dwoma paczkami, których nazwy kończą się na `-x64.zip` oraz
   `-x64-selfcontained.zip` (aktualizator wybiera plik po tej końcówce).

## Licencja i pochodzenie

Projekt bazuje na [mRemoteNG](https://github.com/mRemoteNG/mRemoteNG) i jest rozpowszechniany na
licencji **GPLv2** (patrz `COPYING.txt`). Prawa autorskie do kodu oryginalnego należą do ich autorów.
