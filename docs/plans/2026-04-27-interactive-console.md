# Plan: Consola Interactiva en el Panel de Comandos

**Fecha:** 2026-04-27  
**Contexto:** El panel lateral (`RepoTerminalPanel`) actualmente ejecuta comandos y muestra el output en un div estático de solo lectura. El objetivo es reemplazarlo con una consola interactiva real donde el usuario controla la ejecución, puede seguir escribiendo y puede filtrar/resaltar el output.

---

## Problemas del diseño actual

- El output es de solo lectura — no hay forma de seguir interactuando.
- Ejecutar un comando mata el anterior sin tener una sesión persistente.
- No hay búsqueda ni forma de encontrar errores/URLs dentro de un output largo.
- El usuario no puede tipear comandos propios: solo puede ejecutar los predefinidos.

---

## Objetivo

Convertir la sección de output en una **consola interactiva con shell persistente**:

1. **Consola embebida con input** — el usuario puede tipear y ejecutar comandos libremente.
2. **Inject al hacer clic en un comando** — el botón no ejecuta directamente, sino que pega el comando en el input de la consola (el usuario da Enter).
3. **Shell persistente por repo** — el estado del shell (directorio, variables de entorno) se mantiene entre comandos.
4. **Filtro de líneas** — campo de búsqueda que filtra en tiempo real las líneas visibles.
5. **Highlights automáticos** — colorea palabras clave (errores, URLs, puertos, advertencias) en cada línea.

---

## Arquitectura

### Nuevo servicio: `ShellSessionService`

Gestiona sesiones de shell persistentes, una por repo path.

```csharp
// Services/ShellSessionService.cs
public class ShellSessionService : IDisposable
{
    // Una sesión por repo path
    private readonly Dictionary<string, ShellSession> _sessions = [];

    public ShellSession GetOrCreate(string repoPath, ShellType shell);
    public void Terminate(string repoPath);
    public void TerminateAll();
    public void Dispose();
}

public record ShellSession(
    string RepoPath,
    ShellType Shell,
    Process Process,
    List<ConsoleLine> Lines,
    Action<ConsoleLine>? OnNewLine
);

public record ConsoleLine(string Text, ConsoleLine.LineKind Kind, DateTime Timestamp)
{
    public enum LineKind { Input, Stdout, Stderr, System }
}

public enum ShellType { PowerShell, Cmd, Bash }
```

**Ciclo de vida:**
- Singleton (una instancia en toda la app).
- Al abrir el panel de un repo: `GetOrCreate(repoPath)` — si ya existe la sesión, reutiliza.
- El proceso corre en modo interactivo con `RedirectStandardInput/Output/Error = true`.
- Al cerrar la app el `Dispose` mata todos los procesos.

**Detección automática del shell:**
```
Windows → PowerShell 7 (pwsh.exe) si existe, sino PowerShell 5 (powershell.exe)
Linux   → bash
```
El usuario puede cambiar el shell desde un selector en el header del panel.

### Nuevo componente: `InteractiveConsole`

Reemplaza el `div` de output actual. Es un componente independiente que:

- Muestra un listado de `ConsoleLine` con scroll automático al final.
- Tiene un `MudTextField` en el fondo para tipear comandos.
- Expone un método `InjectCommand(string cmd)` para que los botones peguen el texto sin ejecutar.
- Escucha `session.OnNewLine` para actualizar en tiempo real via `InvokeAsync(StateHasChanged)`.

```
┌─────────────────────────────────────────────────┐
│ Shell: [PowerShell ▾]   [Filtro: ________] [🔴] │ ← header consola
├─────────────────────────────────────────────────┤
│ PS C:\_O\Woffu.Services.Auth>                   │
│ > dotnet run                                    │ ← línea Input (cyan)
│   info: Microsoft.Hosting...                    │
│   Now listening on: http://localhost:5001       │ ← URL highlight
│   warn: Some warning here                       │ ← warn highlight
│   fail: Connection refused                      │ ← error highlight
│ ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓│ ← scrollable, auto-scroll
├─────────────────────────────────────────────────┤
│ $ dotnet build --no-restore█              [↵]   │ ← input
└─────────────────────────────────────────────────┘
```

### Cambio en `RepoTerminalPanel`

El clic en cualquier botón de comando llama a `_console.InjectCommand(cmd.Command)` en lugar de `RunCommandAsync`. El panel deja de gestionar el output directamente.

---

## Feature 1 — Shell persistente

### Implementación

**Inicio del proceso (PowerShell ejemplo):**
```csharp
var psi = new ProcessStartInfo
{
    FileName = "pwsh.exe",                 // o powershell.exe / bash
    Arguments = "-NoLogo -NoExit",         // -NoExit para que no cierre al terminar un comando
    WorkingDirectory = repoPath,
    UseShellExecute = false,
    RedirectStandardInput = true,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true,
};
```

**Lectura asíncrona del output:**
```csharp
process.OutputDataReceived += (_, e) => {
    if (e.Data is not null)
        session.OnNewLine?.Invoke(new ConsoleLine(e.Data, LineKind.Stdout, DateTime.Now));
};
process.ErrorDataReceived += (_, e) => {
    if (e.Data is not null)
        session.OnNewLine?.Invoke(new ConsoleLine(e.Data, LineKind.Stderr, DateTime.Now));
};
process.BeginOutputReadLine();
process.BeginErrorReadLine();
```

**Enviar un comando:**
```csharp
await session.Process.StandardInput.WriteLineAsync(command);
```

**Limitación conocida:** `RedirectStandardInput/Output` no es un PTY real — algunos programas que usan ANSI interactivo (como `top`, `vim`) no funcionarán bien. Para comandos de desarrollo estándar (`dotnet`, `npm`, `git`) es suficiente.

**Alternativa futura (PTY real):** integrar la librería `Pty.Net` (Microsoft) que usa ConPTY en Windows y openpty en Linux. Habilita ANSI colors nativas y programas interactivos. Se puede añadir en una segunda fase sin cambiar la API del servicio.

---

## Feature 2 — Inject al hacer clic (no auto-ejecutar)

**En `InteractiveConsole.razor`:**
```csharp
public void InjectCommand(string command)
{
    _inputText = command;
    // mover foco al input via JS
    _ = JS.InvokeVoidAsync("focusElement", _inputRef);
    StateHasChanged();
}
```

**En los botones de `RepoTerminalPanel.razor`:**
```razor
<MudButton OnClick="() => _console.InjectCommand(cmd.Command)">
    @cmd.Name
</MudButton>
```

El usuario ve el comando en el input, lo puede editar si quiere, y da Enter para ejecutar. Esto es el comportamiento de VS Code cuando abre un terminal y ejecuta una tarea: pega el comando y el usuario confirma.

---

## Feature 3 — Filtro de líneas

Campo de búsqueda sobre el área de output. Filtra en tiempo real las líneas visibles.

**Lógica:**
```csharp
private string _filter = "";

private IEnumerable<ConsoleLine> FilteredLines =>
    string.IsNullOrWhiteSpace(_filter)
        ? _session.Lines
        : _session.Lines.Where(l => l.Text.Contains(_filter, StringComparison.OrdinalIgnoreCase));
```

**UI:**
```razor
<MudTextField @bind-Value="_filter"
              Placeholder="Filtrar output..."
              Adornment="Adornment.Start"
              AdornmentIcon="@Icons.Material.Filled.Search"
              Clearable="true"
              Immediate="true"
              Style="font-size:0.75rem" />
```

**Comportamiento:**
- Al escribir en el filtro, el área de output muestra solo las líneas que contienen el texto.
- El auto-scroll se pausa mientras hay un filtro activo (para no perder el contexto).
- Limpiar el filtro vuelve a mostrar todo y reactiva el auto-scroll.

---

## Feature 4 — Highlights automáticos

En lugar de mostrar líneas como texto plano, cada línea pasa por un proceso de colorización.

### Reglas de highlight por defecto

| Patrón (regex) | Color | Descripción |
|---|---|---|
| `https?://[^\s]+` | `#60a5fa` (azul) | URLs y endpoints |
| `localhost:\d+` | `#34d399` (verde) | Puertos locales |
| `\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}(:\d+)?` | `#34d399` | IPs con puerto |
| `\berror\b`, `\bfail(ed)?\b`, `\bERR\b` | `#f87171` (rojo) | Errores |
| `\bwarn(ing)?\b`, `\bWARN\b` | `#fbbf24` (amarillo) | Advertencias |
| `\binfo\b`, `\bINFO\b` | `#94a3b8` (gris) | Logs informativos |
| `\bsuccess\b`, `\bdone\b`, `\bOK\b` | `#4ade80` (verde claro) | Éxito |
| `\d+ms` | `#c084fc` (primary) | Tiempos de respuesta |

### Implementación

```csharp
// Helpers/ConsoleHighlighter.cs
public static class ConsoleHighlighter
{
    private static readonly (Regex Pattern, string Color)[] Rules =
    [
        (new Regex(@"https?://\S+"), "#60a5fa"),
        (new Regex(@"localhost:\d+"), "#34d399"),
        (new Regex(@"\berror\b|\bfail(ed)?\b|\bERR\b", RegexOptions.IgnoreCase), "#f87171"),
        (new Regex(@"\bwarn(ing)?\b|\bWARN\b", RegexOptions.IgnoreCase), "#fbbf24"),
        (new Regex(@"\bsuccess\b|\bdone\b|\bOK\b", RegexOptions.IgnoreCase), "#4ade80"),
        (new Regex(@"\d+ms"), "#c084fc"),
    ];

    // Devuelve una lista de segmentos (texto, color|null)
    public static IEnumerable<(string Text, string? Color)> Tokenize(string line) { ... }
}
```

En el componente, cada línea se renderiza como `<span>` por segmento:
```razor
@foreach (var (text, color) in ConsoleHighlighter.Tokenize(line.Text))
{
    <span style="@(color is not null ? $"color:{color}" : "")">@text</span>
}
```

### Highlights personalizados

Un `MudChipSet` o lista editable en el header de la consola donde el usuario puede añadir sus propias palabras a resaltar (con color configurable). Se guarda en `LocalStorage` o en la DB junto con los custom commands del repo.

---

## Features adicionales sugeridas

### A — Historial de comandos (↑ / ↓)

Al presionar ↑/↓ en el input, navega por los comandos ejecutados anteriormente en esta sesión (igual que en bash). Lista `_history: List<string>` con un índice.

### B — Tabs de sesión

El panel puede tener varios tabs (cada uno con su propio proceso de shell). Útil para correr el servidor en uno y ejecutar comandos en otro. `ShellSessionService` admite múltiples sesiones por repo, identificadas por un ID de tab.

### C — Timestamps colapsables

Cada línea tiene timestamp. Se muestran al hacer hover o se activan con un toggle.

### D — Copiar línea / copiar selección

Botón "copy" al hacer hover sobre cualquier línea. O botón "copiar todo el output" en el header.

### E — Marcadores / bookmarks de línea

Click en el margen izquierda para marcar una línea. El filtro puede mostrar solo líneas marcadas. Útil para trackear múltiples URLs/puertos cuando arrancan varios servicios.

### F — Auto-scroll inteligente

- Auto-scroll activo por defecto (sigue el output en tiempo real).
- Si el usuario sube manualmente, se desactiva el auto-scroll.
- Aparece un botón flotante "↓ bajar" para volver al final y reactivar.

### G — Indicador de proceso activo

En el header del tab de la consola: indicador visual (punto verde pulsante) si hay un proceso corriendo (`dotnet run`, `npm dev`, etc.). Detectado cuando hay output continuo o cuando el proceso tiene subprocesos activos.

---

## Plan de implementación por fases

### Fase 1 — Consola básica interactiva (MVP)
1. `ShellSessionService` con proceso persistente + `ConsoleLine` model.
2. Registrar en `Program.cs` como Singleton.
3. Componente `InteractiveConsole.razor` con:
   - Listado de líneas con scroll.
   - Input con manejo de Enter.
   - Método `InjectCommand`.
4. Modificar `RepoTerminalPanel` para usar `InjectCommand` en los botones.
5. Tests: `When_ShellSession_*/Then_*.cs`

### Fase 2 — Filtro + Highlights
6. `ConsoleHighlighter` con reglas por defecto.
7. Campo de filtro sobre el output.
8. Renderizado de segmentos coloreados.
9. Tests: `When_ConsoleHighlighter_*/Then_*.cs`

### Fase 3 — UX polish
10. Historial de comandos (↑/↓).
11. Auto-scroll inteligente con botón "bajar".
12. Timestamps on hover.
13. Selector de shell en header.

### Fase 4 — Highlights personalizados + Tabs
14. Highlights custom guardados por repo.
15. Múltiples tabs de sesión por repo.

---

## Archivos a crear/modificar

| Acción | Archivo |
|--------|---------|
| Crear | `Services/ShellSessionService.cs` |
| Crear | `Models/ConsoleLine.cs` |
| Crear | `Models/ShellSession.cs` |
| Crear | `Helpers/ConsoleHighlighter.cs` |
| Crear | `Components/InteractiveConsole.razor` |
| Crear | `Components/InteractiveConsole.razor.cs` |
| Modificar | `Components/RepoTerminalPanel.razor` |
| Modificar | `Components/RepoTerminalPanel.razor.cs` |
| Modificar | `Program.cs` (registrar `ShellSessionService`) |
| Crear | `tests/.../Services/When_ShellSession_*/` |
| Crear | `tests/.../Helpers/When_ConsoleHighlighter_*/` |

---

## Notas técnicas

**¿Por qué no xterm.js en Fase 1?**
xterm.js requiere PTY real (ConPTY en Windows), configuración de WebSocket dedicado y gestión de resize. Añade complejidad significativa. La aproximación con `RedirectStandardInput/Output` cubre el 95% de los casos de uso reales (dotnet, npm, git, docker) y es 100% testeable. xterm.js se puede integrar en Fase 4+ si se necesita soporte completo de ANSI o programas interactivos como `htop`.

**Thread safety:** `ConsoleLine` se agrega desde hilos de lectura de stdout/stderr. Usar `lock` o `Channel<ConsoleLine>` para serializar las actualizaciones antes de `InvokeAsync`.

**Encoding:** Configurar `StandardOutputEncoding = Encoding.UTF8` y `StandardErrorEncoding = Encoding.UTF8` para evitar problemas con caracteres especiales en output de dotnet/npm.
