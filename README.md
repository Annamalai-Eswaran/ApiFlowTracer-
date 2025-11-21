# ApiFlowTracer 🔍

A lightweight debugging companion for .NET developers that captures API flows, database operations, and execution traces in real-time.

## 🌟 What Does It Do?

ApiFlowTracer sits alongside your .NET application and records:
- Every HTTP call your app makes
- All database queries executed
- Exactly where in your code each operation originated
- Complete request/response payloads
- Execution timing and performance metrics

All displayed in a beautiful, real-time web dashboard.

## 🎯 Perfect For

- Debugging complex microservice interactions
- Understanding third-party API integrations
- Performance profiling and optimization
- Documenting API flows for your team
- Teaching and onboarding new developers

## ⚡ Quick Setup

### Step 1: Install (For Now - Manual)

Clone this repository and reference the projects, or build as NuGet packages.

### Step 2: Add to Your Project

In your `Program.cs`:

```csharp
using FlowTracer.Core;

var builder = WebApplication.CreateBuilder(args);

// Add FlowTracer services
builder.Services.AddFlowTracer();

var app = builder.Build();

// Enable FlowTracer middleware
app.UseFlowTracer();

app.Run();
```

### Step 3: Run Your App

The dashboard launches automatically at http://localhost:5050

## 🎨 Dashboard Preview

The dashboard shows a live feed of all operations:

```
🌐 HTTP GET → https://api.example.com/users/123
   📂 UserService.cs:42 in GetUserAsync()
   ⏱️ 145ms | Status: 200 OK
   📤 Request: { "includeDetails": true }
   📥 Response: { "id": 123, "name": "John Doe" }

🗄️ DATABASE SELECT FROM Users WHERE Id = @p0
   📂 UserRepository.cs:28 in FindByIdAsync()
   ⏱️ 8ms | Rows: 1
   
🌐 HTTP POST → https://api.example.com/notifications
   📂 NotificationService.cs:67 in SendAsync()
   ⏱️ 89ms | Status: 201 Created
```

## ⚙️ Configuration Options

Customize behavior with options:

```csharp
builder.Services.AddFlowTracer(config =>
{
    config.DashboardPort = 5050;
    config.TrackHttp = true;
    config.TrackDatabase = true;
    config.TrackMvcActions = true;
    config.BufferSize = 500;
    config.OpenBrowserOnStart = true;
    config.DevelopmentOnly = true;
    
    // Ignore specific patterns
    config.IgnoreUrls = new[] { "/health", "/metrics" };
    
    // Control body capture
    config.CapturePayloads = true;
    config.MaxPayloadKb = 512;
});
```

## 📦 Project Structure

```
ApiFlowTracer-/
├── src/
│   ├── FlowTracer.Core/          # Core tracing engine
│   ├── FlowTracer.WebUI/         # Dashboard interface  
│   └── FlowTracer.Demo/          # Sample application
├── tests/
└── docs/
```

## 🔧 Technical Details

- Built for .NET 6.0+
- Works with ASP.NET Core, MVC, Web API
- Supports EF Core 6.0+
- Zero dependencies on external services
- Thread-safe and async-first
- Minimal performance overhead (~2-5ms per traced operation)

## 🚀 Roadmap

- [ ] NuGet package distribution
- [ ] Visual Studio Code extension
- [ ] Export to various formats (JSON, CSV, Markdown)
- [ ] Request filtering and advanced search
- [ ] Performance analytics and recommendations
- [ ] Distributed tracing across services

## 🤝 Contributing

We welcome contributions! This tool is built by developers who got tired of console logging and debugger stepping.

## 📝 License

MIT License - use freely in your projects!

## 💬 Feedback

Found a bug? Have a feature idea? Open an issue!

---

**Happy Debugging!** 🐛🔨