# UX: Terminal Panel

## Comportamiento

1. **Apertura** — Clic en ícono Terminal de un repo → drawer se abre desde la derecha (560px)

2. **Carga** — Loading spinner mientras detecta tipo + scripts + comandos custom

3. **Lista de comandos** — Agrupados por origen:
   - Detectados (auto-detectados según tipo de proyecto)
   - Scripts (package.json)
   - Personalizados (guardados por usuário)

4. **Ejecución** — Clic en comando:
   - Output reemplaza lista de comandos
   - Cada línea aparece en tiempo real
   - Running = spinner + botón "Stop"

5. **Guardar comando custom** — Formulario nombre + comando → save icon

6. **Cerrar** — X en header o fuera del drawer

## UI

```
┌────────────────────────────────────┐
│ [Terminal]  nombre-repo         [X]│
├────────────────────────────────────┤
│ ─ Detectados ─                     │
│ [Serve] [Build] [Test]            │
│ ─ Scripts (package.json) ─       │
│ [start] [build] [dev]             │
│ ─ Personalizados ─                │
│ [mi-script] [X]                   │
├────────────────────────────────────┤
│ Personalizado                     │
│ [nombre] [comando] [💾]           │
├────────────────────────────────────┤
│ Output                       [🗑]  │
│ ┌──────────────────────────────┐   │
│ │ > npm run dev                 │   │
│ │ ℹ️  Ready...                  │   │
│ │ ℹ️  Local: http://localhost   │   │
│ └──────────────────────────────┘   │
└────────────────────────────────────┘
```