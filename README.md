# Dashcam Video Repair

Aplikacja desktopowa Windows do naprawy uszkodzonych plików wideo z kamerek samochodowych (MOV/MP4).

Nagrania z kamerek samochodowych często ulegają uszkodzeniu z powodu nagłej utraty zasilania, co skutkuje brakującymi atomami moov lub niekompletnymi strukturami plików. Ta aplikacja automatyzuje proces naprawy przy użyciu FFmpeg z wieloetapową strategią awaryjną.

## Funkcje

- **Przeciągnij i upuść** — dodawaj pliki i foldery bezpośrednio do okna aplikacji
- **Wieloetapowa naprawa** — FastRepair → FullRepair → UntruncRepair z automatycznym przełączaniem
- **Wykrywanie brakującego atomu moov** — pomija nieskuteczne strategie i przechodzi bezpośrednio do UntruncRepair
- **Walidacja wyjścia** — ffprobe sprawdza czy naprawiony plik ma czas trwania > 0 i strumień wideo
- **Przetwarzanie wsadowe** — naprawiaj wiele plików jednocześnie z paskami postępu
- **Ciemny motyw** — interfejs przystosowany do pracy w słabym oświetleniu
- **Logowanie** — pełna historia operacji w plikach logów (Serilog)
- **Przenośność** — self-contained build, nie wymaga zainstalowanego .NET Runtime

## Strategie naprawy

| Strategia | Opis | Kiedy używana |
|-----------|------|---------------|
| **FastRepair** | `ffmpeg -c copy` — kopiowanie strumieni bez rekodowania | Zawsze jako pierwsza próba |
| **FullRepair** | `ffmpeg -c:v libx264 -c:a aac` — pełne rekodowanie | Gdy FastRepair zawiedzie (bez błędu moov) |
| **UntruncRepair** | `untrunc` + plik referencyjny → FFmpeg re-process | Gdy wykryto brak atomu moov |

## Wymagania

- Windows 10/11 (x64)
- [FFmpeg](https://www.gyan.dev/ffmpeg/builds/) — `ffmpeg.exe` + `ffprobe.exe`
- [Untrunc](https://github.com/anthwlock/untrunc) — opcjonalnie, do naprawy plików z brakującym atomem moov

## Szybki start

### Gotowa paczka (publish/)

1. Rozpakuj folder `publish/`
2. Uruchom `DashcamVideoRepair.exe`
3. FFmpeg, untrunc i plik referencyjny są już skonfigurowane
4. Dodaj uszkodzone pliki i kliknij **Rozpocznij naprawę**

### Kompilacja ze źródeł

```bash
# Wymagany .NET 8 SDK
dotnet build DashcamVideoRepair.sln

# Uruchomienie testów
dotnet test DashcamVideoRepair.Tests/DashcamVideoRepair.Tests.csproj

# Publikacja self-contained
dotnet publish DashcamVideoRepair/DashcamVideoRepair.csproj -c Release -r win-x64 --self-contained true -o publish
```

## Konfiguracja

Aplikacja automatycznie szuka `ffmpeg.exe` w swoim katalogu i w systemowym PATH. Jeśli nie znajdzie, poprosi o wskazanie ścieżki.

Ustawienia (Ustawienia → przycisk w aplikacji):
- **Ścieżka Untrunc** — ścieżka do `untrunc.exe`
- **Plik referencyjny** — prawidłowy plik wideo z tej samej kamerki (wymagany do UntruncRepair)

Konfiguracja zapisywana jest w `config.json` w katalogu aplikacji. Obsługuje ścieżki względne.

## Architektura

```
DashcamVideoRepair/
├── Models/          # FileStatus, RepairQueueItem, RepairResult, AppConfig
├── Infrastructure/  # FFmpegProcess, UntruncProcess, ConfigStore, Validators
├── Services/        # VideoRepairService, ToolDiscoveryService
├── ViewModels/      # MainViewModel, SettingsViewModel (MVVM)
└── Views/           # MainWindow, SettingsWindow (WPF)
```

- **Clean Architecture** — UI / Application / Infrastructure
- **MVVM** — ViewModels z INotifyPropertyChanged i RelayCommand
- **Dependency Injection** — Microsoft.Extensions.DependencyInjection
- **Async/Await** — wszystkie operacje naprawy na wątkach tła

## Licencja

Projekt prywatny.
