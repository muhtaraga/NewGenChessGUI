# ChessGUI

Satranç analizi için geliştirilen, ChessBase / Nibbler / SCID tarzı Windows masaüstü uygulaması.

## Özellikler

- Herhangi bir **UCI motoru** ekleyip çoklu motorla canlı analiz yapma (Stockfish, Lc0, Komodo…)
- Motor yöneticisi: motor ekleme/kaldırma, UCI seçeneklerini düzenleme
- Tahta üzerinde oynama, pozisyon düzenleyici (FEN kurulumu)
- Hamle listesi, varyant ağacı, NAG işaretleri
- Oyun raporu: hamle sınıflandırması ve doğruluk (accuracy) özeti
- **SQLite** tabanlı oyun veritabanı, PGN içe aktarma
- **Açılış kitabı** ve ECO sınıflandırma
- Saat (clock) desteği, ses efektleri
- Modern, koyu temalı arayüz (açık tema desteğiyle)

## Teknoloji Yığını

| Katman | Seçim |
|--------|-------|
| Runtime | .NET 8 (LTS) |
| UI | WPF + XAML (MVVM, CommunityToolkit.Mvvm) |
| Veritabanı | SQLite |
| Satranç mantığı | Kendi çekirdeğimiz (bitboard tabanlı hamle üretimi, perft ile doğrulanmış) |
| Motor protokolü | UCI (özel ince katman) |
| Test | xUnit |

## Çözüm Yapısı

```
ChessGUI.sln
├── src/
│   ├── ChessGUI.Core/       Tahta, hamle üretimi, notasyon (FEN/SAN/PGN), oyun ağacı
│   ├── ChessGUI.Engine/     UCI motor entegrasyonu (process yönetimi, analiz oturumu)
│   ├── ChessGUI.Data/       SQLite kalıcılık katmanı, PGN toplu içe aktarma
│   ├── ChessGUI.Openings/   ECO sınıflandırma
│   └── ChessGUI.App/        WPF arayüzü (MVVM: Views, ViewModels, Services)
└── tests/
    ├── ChessGUI.Core.Tests/
    ├── ChessGUI.Engine.Tests/
    ├── ChessGUI.Data.Tests/
    └── ChessGUI.Openings.Tests/
```

`ChessGUI.Core` hiçbir katmana bağımlı değildir; bu sayede çekirdek satranç mantığı UI olmadan test edilebilir.

## Gereksinimler

- Windows
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- (Opsiyonel) Analiz için bir UCI motoru, örn. [Stockfish](https://stockfishchess.org/)

## Kurulum ve Çalıştırma

```bash
git clone https://github.com/muhtaraga/NewGenChessGUI.git
cd NewGenChessGUI
dotnet build
dotnet run --project src/ChessGUI.App
```

## Test Çalıştırma

```bash
dotnet test
```

## Katkı

Bu proje kişisel/hobi amaçlı geliştirilmektedir. Hata bildirimi ve öneriler için Issues sekmesini kullanabilirsiniz.

## Lisans

[MIT](LICENSE)
