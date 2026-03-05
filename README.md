# TodoSharp

Telegram To-Do bot — C# (.NET 8.0) da yozilgan, SQLite bazali, inline keyboard orqali boshqariladigan vazifalar boti.

## Funksionallik

- **Vazifalar** — qo'shish, tahrirlash, o'chirish, bajarish
- **Prioritet** — Past / Oddiy / Yuqori / Shoshilinch
- **Muddat** — har bir vazifaga deadline belgilash
- **Timer** — vazifa ustida ishlash vaqtini o'lchash (start/stop)
- **Statistika** — kunlik, haftalik, oylik va umumiy hisobot
- **Forward/Media** — forward qilingan xabar yoki audio/video/fayl avtomatik vazifaga aylanadi
- **Kanal qo'llab-quvvatlash** — kanalga admin qilib qo'shsangiz, kanal ichida ham ishlaydi
- **Token saqlash** — bot tokenini bazaga saqlaydi, qayta kiritish shart emas

## O'rnatish

**.NET 8.0 SDK** kerak: https://dotnet.microsoft.com/download

### Windows
```powershell
powershell -ExecutionPolicy Bypass -File install.ps1
```

### Linux / macOS
```bash
chmod +x install.sh && ./install.sh
```

Install vaqtida bot token so'raladi va avtomatik ishga tushadi.

## Boshqarish

```bash
todosharp start     # Botni fonda ishga tushirish
todosharp stop      # To'xtatish
todosharp restart   # Qayta ishga tushirish
todosharp status    # Holatni ko'rish
todosharp log       # Loglarni ko'rish
todosharp run       # Terminal rejimida ishga tushirish
```

## Texnologiyalar

- C# / .NET 8.0
- Telegram.Bot 22.0.0
- Entity Framework Core + SQLite
- Inline Keyboard UI

## Litsenziya

MIT
