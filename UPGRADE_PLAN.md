# Plan pełnego merge'u upstreamu do forka

**Branch roboczy:** `upgrade/upstream-merge` (odgałęziony z `main` @ `71006483`)
**Cel upstreamu:** `upstream/v1.78.2-dev` @ `9b8b2995`
**Wspólny przodek:** `ee88e835` (2025-10-22)
**Różnica:** 1468 commitów do tyłu, 17 do przodu

Dokument opisuje **plan**, nie wykonaną pracę. Merge nie został jeszcze uruchomiony.

---

## POSTĘP (stan na 2026-11-16)

| Krok | Status | Uwagi |
|---|---|---|
| 0. Zabezpieczenie | ✔ | branch `upgrade/upstream-merge`, tag backup |
| 1. Narzędzia | ✔ | SDK 11.0.100 do `%LOCALAPPDATA%\Microsoft\dotnet11`; VS MSBuild 18.7.8 + `MSBuildSDKsPath`; **UWAGA: `dotnet build` nie działa (COM), trzeba MSBuild z VS** |
| 2. Merge | ✔ | commit `893c1b15`, 40 konfliktów |
| 3. Konflikty | ✔ | duplikaty → upstream; funkcje forka → nasze; usunięto `DotNetRuntimeCheck` |
| 4. Build | ✔ | `mRemoteNG.dll` pod `net11.0-windows...` |
| 5. Testy | ✔ | **2931/2931** (dotnet 11, MSBuild VS + SDK 11) |
| 6. Weryfikacja funkcji | ✔ | About (SHA + tag), GitHub updater (live API), self-contained bez DOTNET_ROOT |
| 7. Decyzja o wydaniu | ⏳ | **OCZEKUJE NA DECYZJĘ UŻYTKOWNIKA** |
| 8. Merge do main | ⏳ | **OCZEKUJE NA DECYZJĘ UŻYTKOWNIKA** |

### Szczegóły wykonania

- Konflikty rozwiązane grupami: 27 plików duplikatów poprawek → upstream;
  13 plików z funkcjami forka → ręczne połączenie (m.in. `ProgramRoot`, `frmMain`,
  `frmOptions`, `FrmAbout`, `AppUpdater`).
- Automatyczny merge zduplikował konstruktor `RootNodeInfo` i właściwości w
  `SerializableConnectionInfoAllPropertiesOfType` — usunięte.
- Usunięto własne testy aktywnej zakładki (`ConnectionTabFocusRestoreTests`,
  `ConnectionTabActivationTests`) — upstream ma `DockPaneStripNGTests` (12 testów).
- Dodano `?.` w `ConnectionWindow.Prot_Event_Closed` — regresja upstreamu (`InterfaceControl == null`).
- **Self-contained:** 524 pliki / 237 MB, runtime 11.0.0 w środku, startuje bez zewnętrznego .NET
  (sprawdzone z `DOTNET_ROOT` wskazującym na pusty katalog).
- **FD:** wymaga .NET 11 Desktop Runtime; bez niego apphost pokazuje natywny dialog (zgodnie
  z dokumentacją upstreamu).
- SHA commita w About: `Fork build 20261116e-v1.78.2-NB (<sha>) · framework-dependent`.
- Linki About → COPYING/CHANGELOG/CREDITS na branchu `main`: HTTP 200.
- `user.config` z wersją 10.0.0.0 jest automatycznie normalizowany do 11.0.0.0 przy starcie.

### Blokery / uwagi dla wydania (krok 7)

1. **`BuildTag` nadal `20261116e-v1.78.2-NB`** (wydanie FD/SC z .NET 9 jest aktualnie opublikowane).
   Przed wydaniem .NET 11 **trzeba podbić tag**, inaczej aktualizator nie wykryje nowej wersji.
2. Nowy tag releases musi mieć oba zipy z sufiksami `-x64.zip` i `-x64-selfcontained.zip`.
3. Poprzednie wydanie usunąć dopiero po opublikowaniu nowego (zasada jednego release'u).
4. Paczki zbudowane w trakcie testów (`artifacts\sc11-x64`) są tymczasowe i nie są przeznaczone
   do publikacji — przed wydaniem trzeba zbudować je na nowo z podbitym `BuildTag`.

---

## Sucha analiza (już wykonana)

`git merge-tree --write-tree main upstream/v1.78.2-dev` pokazuje **40 konfliktów**:

- 79 plików zmienionych po naszej stronie, 564 po stronie upstreamu, 59 wspólnych.
- Z 59 wspólnych 40 faktycznie się kłóci — reszta scali się automatycznie.
- 201 nowych plików z upstreamu (m.in. 6 projektów wtyczek, dokumentacja).

**Kluczowe odkrycie:** upstream sam naprawił większość błędów, które poprawiliśmy w tym forkach.
Nasze 17 commitów to w dużej mierze duplikaty. Dotyczy to m.in.:

| Nasza poprawka | Odpowiednik upstreamu | Data |
|---|---|---|
| Aktywna zakładka po „Nie" | PR #3408 (`DockPaneStripNG.cs`) | 2026-09-09 |
| Crash zakładki podczas łączenia | `b5691683` | 2026-11-06 |
| Ciemny pasek tytułu | `355d88f5` | 2026-07-02 |
| Kolory w CSV | `4682e2c7` | 2026-10-29 |
| `confVersion` w XML | `edc007c4` | 2026-10-21 |
| Logika update check | `7ec5eb84` | 2026-02-24 |
| Strony Opcji przy 1. otwarciu | `045b0faf` | 2026-11-09 |

**Wniosek:** celem merge'u nie jest „przenieść nasze 17 commitów", tylko **zachować 5 unikalnych
funkcji forka** i odrzucić duplikaty. Próba zachowania wszystkich 17 skończy się konfliktami
w plikach, które obie strony poprawiły na różne sposoby.

---

## Co jest unikalne w forku i MUSI przetrwać

1. **Przenośny `user.config`** — `App/Info/UserSettingsFile.cs` + wywołania w `ProgramRoot`,
   `SettingsSaver`, `FrmOptions`, normalizacja wersji runtime.
2. **GitHub updater** — `App/Checks/ForkUpdateChannel.cs`, `GitHubReleaseParser.cs`,
   `ReleaseTag.cs`, `UpdateInstaller.cs`, przerobiony `AppUpdater.cs`, `UpdateInfo.cs`.
3. **Restart po zmianie motywu** — `ProgramRoot.RestartApplication`, `FrmOptions`,
   `FrmMain.SkipCloseConfirmation`, `ThemePage`.
4. **Widoczna tożsamość buildu w About** — SHA commita (`GenerateGitInfo` w csproj),
   linia `Fork build <tag>`, linki do repozytorium forka.
5. **README i proces wydawania** — jeden release z dwoma zipami, zasada podbijania `BuildTag`.

Uwaga: upstream **usunął** `App/Checks/DotNetRuntimeCheck.cs` i używa stałej kompilacyjnej
`SELF_CONTAINED` zamiast sprawdzania w czasie działania. Nasza wersja tego pliku jest zmodyfikowana
i w `merge-tree` pojawia się jako `modify/delete` — decyzja w kroku 4.

---

## Kroki

### Krok 0 — zabezpieczenie (zrobione)

- Nowy branch `upgrade/upstream-merge` — ✔
- Tag `backup-v1.78.2-dev-20261116` dla usuniętej gałęzi — ✔
- `main` nietknięty, czysty — ✔

### Krok 1 — narzędzia (bloker)

Do zbudowania kodu upstreamu potrzebne są nowsze narzędzia:

| Wymagane | Aktualnie |
|---|---|
| .NET SDK **11.0.100** | 10.0.301 |
| Visual Studio **2026 (18.12)** / MSBuild 18.12 | 18.7.8 |

**Akcja:** zainstalować SDK 11 i VS 2026 (18.12). Bez tego build się nie uda —
upstreamowy `mRemoteNG.csproj` ma `<TargetFramework>net11.0-windows10.0.26100.0</TargetFramework>`.

**Bramka:** `dotnet --list-sdks` pokazuje 11.0.100 i `MSBuild.exe -version` pokazuje ≥ 18.12.

### Krok 2 — uruchomienie merge'u

```powershell
git switch upgrade/upstream-merge
git merge --no-commit --no-ff upstream/v1.78.2-dev
```

`--no-commit` pozwala rozwiązać konflikty i zweryfikować całość przed jednym commitem.
W razie katastrofy: `git merge --abort` — branch `main` pozostaje nienaruszony.

### Krok 3 — grupowanie konfliktów (nie rozwiązuj po kolei!)

Konflikty dzielą się na 4 grupy i każda ma inną strategię:

**Grupa A — duplikaty naszych poprawek → brać wersję upstreamu (`--theirs`)**

Pliki, które upstream poprawił sam i to lepiej (ma testy):

- `UI/Tabs/ConnectionTab.cs`, `UI/Window/ConnectionWindow.cs`
- `Themes/ThemeManager.cs`, `App/NativeMethods.cs`
- `Config/Serializers/ConnectionSerializers/Csv/*`, `Xml/*`
- `UI/Forms/OptionsPages/*` (wszystkie)
- `Tools/ExternalTool.cs`, `Config/DataProviders/FileBackupCreator.cs`
- `Credential/CredentialRecordTypeConverter.cs`
- testy: `SecureXmlHelperTests.cs`, `ExternalToolTests.cs`, `TabColorConverterTests.cs`,
  `OptionsFormTests.cs`, `OptionsAdvancedPageTests.cs`, `ConfigWindowGeneralTests.cs`

**Grupa B — nasze unikalne funkcje → brać naszą wersję (`--ours`), potem dopasować do API upstreamu**

- `App/Checks/AppUpdater.cs`, `UpdateInfo.cs`
- `App/ProgramRoot.cs`, `App/Shutdown.cs`, `App/Runtime.cs`
- `App/Info/GeneralAppInfo.cs`
- `UI/Window/UpdateWindow.cs`, `UI/Forms/FrmAbout.cs`, `UI/Forms/frmOptions.cs`
- `UI/Forms/frmMain.cs` (tylko `SkipCloseConfirmation` — reszta od upstreamu)
- nowe pliki (bez konfliktu, ale wymagają podpięcia): `ForkUpdateChannel.cs`,
  `GitHubReleaseParser.cs`, `ReleaseTag.cs`, `UpdateInstaller.cs`, `UserSettingsFile.cs`

**Grupa C — pliki konfiguracyjne → rozwiązać ręcznie**

- `Directory.Packages.props` — wziąć wersje pakietów upstreamu (szczególnie CEF 156, .NET 11),
  dodać ewentualne nasze zależności.
- `mRemoteNG/mRemoteNG.csproj` — wziąć `net11.0-*` i `SupportedOSPlatformVersion 10.0.17763.0`
  od upstreamu, **zachować nasz target `GenerateGitInfo`** (SHA commita).
- `README.md` — zachować nasz README forka, ewentualnie dodać informację o .NET 11.
- `App/Checks/DotNetRuntimeCheck.cs` (modify/delete) — **usunąć** i przenieść naszą logikę
  self-contained detection do `ForkUpdateChannel` (które już ma `IsSelfContainedBuild`).
  Upstream używa `SELF_CONTAINED` w czasie kompilacji; nasz runtime detection można zostawić
  jako uzupełnienie, ale nie może wisić martwy plik.

**Grupa D — pliki z drobnymi różnicami → brać upstream, dodać naszą linię**

- `App/Info/GeneralAppInfo.cs` — upstream może mieć zmiany w `GetApplicationVersion`;
  zachować nasze `GetVersion`/`GetBuildNumber`/`GetCommitHash`.
- `App/NativeMethods.cs` — zachować nasz `UseImmersiveDarkMode` tylko jeśli upstream go nie ma.

### Krok 4 — pierwszy build (bez testów)

```powershell
MSBuild.exe mRemoteNG.sln -p:Configuration=Debug -p:Platform=x64
```

Cel: złapać błędy kompilacji wynikające ze zmian API (np. usunięty `DotNetRuntimeCheck`,
przeniesione klasy, nowe przestrzenie nazw pluginów). **Nie przechodzimy dalej, póki się nie buduje.**

### Krok 5 — pełny zestaw testów

```powershell
MSBuild.exe mRemoteNGTests\mRemoteNGTests.csproj -p:Configuration=Debug -p:Platform=x64
vstest.console.exe mRemoteNGTests\bin\x64\Debug\mRemoteNGTests.dll
```

Upstream ma własne testy (178 plików testowych). Nasze 17 commitów dodało 57 testów.
Po merge'u liczba będzie inna niż 2059 — to OK, ważne żeby **zero failures**.

Jeśli test y nasze duplikują upstreamowe (np. `ConnectionTabFocusRestoreTests` vs upstreamowy
`DockPaneStripNGTests`) — usunąć nasze, zostawić upstreamowe.

### Krok 6 — weryfikacja funkcjonalna (5 unikalnych funkcji)

| Funkcja | Jak sprawdzić |
|---|---|
| `user.config` przenośny | Uruchomić z katalogu z plikiem i bez; zmienić ścieżkę i sprawdzić restore |
| GitHub updater | `Check for Updates` → musi pokazać tag release; sprawdzić wybór FD/SC |
| Restart po motywie | Opcje → motyw → OK → „Restart now?" → nowy proces |
| SHA w About | About → linia `Fork build <tag> (<sha>)`; linki License/Changelog/Credits → 200 |
| Jeden release | Sprawdzić stronę releases (nie publikować jeszcze) |

Uwaga: po mergu zmieni się `.NET 9 → 11`, więc test self-contained trzeba powtórzyć
na maszynie bez runtime 11 (lub przynajmniej sprawdzić `runtimeconfig.json`).

### Krok 7 — decyzja o wydaniu (nie automatycznie!)

Po udanym merge'u **zapytać użytkownika**, czy:

1. Wydać nowy tag z paczkami .NET 11 (podbij `BuildTag`, np. `20261117-v1.78.2-NB`),
2. Czy najpierw dłużej testować na branchu `upgrade/upstream-merge`.

Nie publikować release'u bez potwierdzenia — poprzednie wydania były podmieniane i to
już raz spowodowało, że użytkownicy nie dostali poprawki.

### Krok 8 — scalać do `main`

Dopiero po kroku 7:

```powershell
git switch main
git merge upgrade/upstream-merge
git push origin main
```

---

## Ryzyka

| Ryzyko | Mitygacja |
|---|---|
| Konflikty w 40 plikach | Strategie z kroku 3 — nie rozwiązuj „na oko" |
| Usunięty `DotNetRuntimeCheck.cs` | Krok 3 grupa C |
| Zmiana API upstreamu (np. `ProgramRoot`) | Krok 4 przed testami |
| Nowe projekty wtyczek zmieniają strukturę buildu | Krok 4 obejmie całe solution |
| Duplikaty testów | Krok 5 |
| Brak narzędzi .NET 11 / VS 18.12 | Krok 1 — **bloker** |
| Nieświadome nadpisanie naszych unikalnych funkcji | Krok 6 — checklista 5 funkcji |

## Czego NIE robić

- Nie robić `git merge -X theirs` na całym merge'u — wyczyści unikalne funkcje forka.
- Nie robić `git rebase` 17 commitów na upstream — duplikaty poprawek wybuchną konfliktami
  w tych samych plikach, a historia i tak się nie uporządkuje.
- Nie podbijać `BuildTag` przed krokiem 7.
- Nie publikować wydania z tego brancha bez potwierdzenia.
