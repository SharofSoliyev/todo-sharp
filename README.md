# TodoSharp

Telegram To-Do bot — C# (.NET 8.0), SQLite, inline keyboard bilan boshqariladigan vazifalar boti.

## Tezkor o'rnatish

```bash
git clone https://github.com/SharofSoliyev/todo-sharp.git
cd todo-sharp
```

**Windows** — `setup.cmd` ni ikki marta bosing yoki:
```powershell
.\setup.cmd
```

**Linux / macOS:**
```bash
chmod +x setup.sh && ./setup.sh
```

Hamma narsa avtomatik: .NET SDK, build, token so'rash, bot ishga tushirish.

## Boshqarish

```bash
todosharp start     # Fonda ishga tushirish
todosharp stop      # To'xtatish
todosharp restart   # Qayta ishga tushirish
todosharp status    # Holatni ko'rish
todosharp log       # Loglarni ko'rish
todosharp run       # Terminal rejimida ishga tushirish
```

## Funksionallik

- **Vazifalar** — qo'shish, tahrirlash, o'chirish, bajarish
- **Prioritet** — Past / Oddiy / Yuqori / Shoshilinch
- **Muddat** — har bir vazifaga deadline belgilash
- **Timer** — vazifa ustida ishlash vaqtini o'lchash (start/stop)
- **Statistika** — kunlik, haftalik, oylik va umumiy hisobot
- **Forward/Media** — forward qilingan xabar yoki audio/video/fayl avtomatik vazifaga aylanadi
- **Kanal** — kanalga admin qilib qo'shsangiz, kanal ichida ham ishlaydi
- **Token saqlash** — bot tokenini bazaga saqlaydi, qayta kiritish shart emas

## Loyiha strukturasi

```
todo-sharp/
├── src/
│   ├── Data/
│   │   └── AppDbContext.cs        # EF Core + SQLite
│   ├── Models/
│   │   ├── TodoItem.cs            # Vazifa modeli
│   │   ├── UserState.cs           # Foydalanuvchi holati
│   │   └── BotConfig.cs           # Sozlamalar (token)
│   ├── Services/
│   │   ├── BotService.cs          # Asosiy bot logikasi
│   │   └── KeyboardBuilder.cs     # Inline keyboard UI
│   ├── Program.cs                 # Entry point
│   ├── TodoBot.csproj             # Loyiha fayli
│   └── todosharp.ps1              # Windows boshqaruv skripti
├── scripts/
│   ├── install.ps1                # Windows/macOS/Linux installer (PowerShell)
│   └── install.sh                 # Linux/macOS installer (Bash)
├── setup.cmd                      # Windows: bir bosish bilan o'rnatish
├── setup.sh                       # Linux/macOS: bir buyruq bilan o'rnatish
├── LICENSE
└── README.md
```

## Texnologiyalar

- C# / .NET 8.0
- [Telegram.Bot](https://github.com/TelegramBots/Telegram.Bot) 22.0.0
- Entity Framework Core + SQLite
- Inline Keyboard UI

## Litsenziya

[MIT](LICENSE)
