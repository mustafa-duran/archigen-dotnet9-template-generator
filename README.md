# Archigen - .NET 9 Template Generator

## 🤖 AI Destekli Geliştirme Hakkında

Bu proje, **AI araçları kullanılarak geliştirilmiştir**. AI teknolojisinin burada kullanılmasının amacı yalnızca geliştirme sürecini hızlandırmak ve iş kolaylaştırmaktır. AI, kod yazma sürecinde yardımcı bir araç olarak kullanılmış olup, projenin temel mantığı, mimarisi ve kalitesi tamamen insan denetimi altında geliştirilmiştir.

## 📋 Proje Hakkında

**Archigen**, .NET 9 tabanlı Clean Architecture projelerini hızlı bir şekilde oluşturmak ve CRUD operasyonlarını otomatik olarak generate etmek için geliştirilmiş bir **kod üretici aracıdır**. Bu araç, modern .NET geliştirme standartlarını takip eden, ölçeklenebilir ve maintainable projeler oluşturmanızı sağlar.

## ✨ Özellikler

- 🏗️ **Clean Architecture Template**: Katmanlı mimari ile proje şablonu oluşturma
- 🔄 **Otomatik CRUD Üretimi**: Entity tanımlamalarından tam CRUD operasyonları oluşturma
- 🎯 **Interactive CLI**: Kullanıcı dostu komut satırı arayüzü
- 🛡️ **Security Integration**: JWT tabanlı authentication/authorization desteği
- 📊 **Repository Pattern**: Generic repository pattern implementasyonu
- 🧩 **Modüler Yapı**: Bağımsız katmanlar ve dependency injection
- 📱 **Web API**: RESTful API controller'ları otomatik üretimi
- 🎨 **Template Customization**: Özelleştirilebilir kod şablonları

## 🏛️ Proje Mimarisi

```
├── Archigen.Cli/          # Komut satırı arayüzü
├── Archigen.Core/         # Temel sınıflar ve modeller
├── Archigen.Generator/    # Kod üretimi mantığı
└── template/              # Proje şablonları
    ├── core/             # Core katman şablonları
    └── project/          # Ana proje şablonları
```

### Template Katman Yapısı

- **Core.Application**: CQRS pattern, MediatR, AutoMapper
- **Core.Persistence**: Entity Framework Core, Repository Pattern
- **Core.Security**: JWT, Authentication, Authorization
- **Core.CrossCuttingConcerns**: Logging, Exception Handling
- **Core.Localization**: Çoklu dil desteği
- **Core.Mailing**: Email servisleri
- **Project.Domain**: Domain entities ve business rules
- **Project.Application**: Use cases ve business logic
- **Project.Infrastructure**: External services
- **Project.Persistence**: Database context ve repositories
- **Project.WebAPI**: RESTful API endpoints

## 🚀 Kurulum

### Gereksinimler

- .NET 9.0 SDK
- Visual Studio 2022 veya VS Code

### Kurulum Adımları

1. **Repository'yi klonlayın:**

```bash
git clone https://github.com/mustafa-duran/archigen-dotnet9-template-generator.git
cd archigen-dotnet9-template-generator
```

2. **Projeyi build edin:**

```bash
cd archigen
dotnet build
```

3. **CLI'yı çalıştırın:**

```bash
dotnet run --project src/Archigen.Cli
```

## 💻 Kullanım

### İnteraktif Menü

```bash
dotnet run --project src/Archigen.Cli
```

### Yeni Proje Oluşturma

```bash
dotnet run --project src/Archigen.Cli new --name MyProject --path ./output
```

### CRUD Operasyonları Üretme

```bash
dotnet run --project src/Archigen.Cli crud --entity Product --props "Name:string,Price:decimal,Description:string"
```

### Kullanılabilir Komutlar

- `menu` - İnteraktif menüyü başlatır
- `new` - Yeni proje oluşturur
- `crud` - CRUD operasyonları üretir
- `parse-entity` - Mevcut entity'yi analiz eder
- `layout-test` - Proje layout'unu test eder

## 📁 Üretilen Proje Yapısı

```
MyProject/
├── MyProject.Domain/
│   └── Entities/
├── MyProject.Application/
│   ├── Features/
│   └── Services/
├── MyProject.Infrastructure/
│   └── Adapters/
├── MyProject.Persistence/
│   ├── Contexts/
│   ├── Repositories/
│   └── EntityConfigurations/
└── MyProject.WebAPI/
    ├── Controllers/
    └── Program.cs
```

## 🛠️ Özelleştirme

### Template Dosyaları

`template/` klasöründeki dosyaları düzenleyerek kendi şablonlarınızı oluşturabilirsiniz.

### Entity Özelleştirme

Entity tanımlamalarında desteklenen tipler:

- `string`, `int`, `decimal`, `bool`, `DateTime`
- `Guid` (ID tipi olarak)
- Navigation properties

---

**Not**: Bu araç, tekrarlayan kod yazma işlemlerini otomatikleştirerek geliştirici verimliliğini artırmayı amaçlamaktadır. Üretilen kodların proje gereksinimlerinize uygun olarak gözden geçirilmesi önerilir.
