# AGENTS.md — Regole di Sviluppo

> Progetto: MobileGame 2D  
> Engine: Unity 6 (6000.4.6f1)  
> Target: Android  
> Ultima revisione: 2026-08-31

---

## 1. File `.meta` — DIVIETO ASSOLUTO DI MODIFICA

- **Non toccare, rinominare, spostare o eliminare mai un file `.meta`** generato da Unity.
- I file `.meta` sono gestiti esclusivamente dall'editor Unity; ogni modifica manuale corrompe i GUID e rompe i riferimenti tra asset.
- In caso di conflitti Git su file `.meta`, risolvere sempre mantenendo la versione già presente nel repository (ours) senza editare il contenuto.
- Aggiungere la riga seguente al `.gitattributes` per evitare diff indesiderati sui meta:

  ```gitattributes
  *.meta merge=union
  ```

---

## 2. Input — New Input System (solo ed esclusivamente)

- Usare **esclusivamente** il package `com.unity.inputsystem` (New Input System).
- Il vecchio `Input` class (`UnityEngine.Input`) è **proibito**. Non chiamare mai `Input.GetTouch`, `Input.touches`, `Input.GetAxis`, ecc.
- Tutti i controlli touch devono essere implementati tramite:
  - `InputAction` + `InputActionAsset` (file `.inputactions`)
  - Oppure `EnhancedTouch.Touch` / `EnhancedTouch.Finger` per lettura diretta dei tocchi multi-finger.
- Abilitare Enhanced Touch all'avvio del gioco:

  ```csharp
  using UnityEngine.InputSystem.EnhancedTouch;
  // in Awake() o OnEnable():
  EnhancedTouchSupport.Enable();
  ```

- Il `Player Input` component può essere usato solo se collegato a un `InputActionAsset` versionato dentro `Assets/Settings/Input/`.
- Le azioni di input devono essere definite in file `.inputactions`, mai hard-coded.

---

## 3. Framerate — Bloccato a 60 FPS all'avvio

- Impostare il framerate **prima di qualsiasi altra logica di gioco**, nel metodo `Awake()` della classe di bootstrap (es. `GameBootstrap.cs`):

  ```csharp
  Application.targetFrameRate = 60;
  QualitySettings.vSyncCount  = 0; // disabilitare VSync; il target rate ha la precedenza
  ```

- Questo blocco deve trovarsi nello script con `[DefaultExecutionOrder(-1000)]` per garantire l'esecuzione anticipata.
- Non modificare `targetFrameRate` in altri punti del codice senza documentazione esplicita e approvazione.
- In `ProjectSettings/QualitySettings.asset` il valore di VSync Count deve rimanere a `0` per ogni Quality Level destinato ad Android.

---

## 4. Allocazioni Dinamiche — Zero GC nei Loop (GC-Friendly)

Regola fondamentale: **nessuna allocazione heap all'interno di `Update()`, `FixedUpdate()`, `LateUpdate()`, coroutine a cadenza fissa o qualsiasi altro loop hot-path**.

### Pattern consentiti

| Tecnica | Uso |
|---|---|
| `struct` invece di `class` per dati temporanei | Evita allocazioni heap |
| Pool di oggetti (`ObjectPool<T>`) | Riuso di istanze di `GameObject`/`MonoBehaviour` |
| `List<T>.Clear()` + riuso della lista pre-allocata | Mai `new List<T>()` nel loop |
| `static readonly` array/buffer | Buffer riutilizzabili dichiarati una volta |
| `StringBuilder` pre-allocato | Per la UI testuale aggiornata ogni frame |
| `Span<T>` / `NativeArray<T>` | Accesso a dati senza boxing |

### Pattern vietati nel loop

```csharp
// VIETATO in Update/FixedUpdate:
var list    = new List<Enemy>();          // allocazione heap
var message = "Score: " + score;         // string concatenation -> allocazione
GetComponents<Collider2D>();              // restituisce array -> allocazione
StartCoroutine(SomeRoutine());            // in loop ad alta frequenza
gameObject.GetComponent<Rigidbody2D>();  // chiamata costosa (ok in Awake, non in Update)
```

```csharp
// Versione corretta:
// Pre-alloca in Awake():
private readonly List<Enemy> _enemyBuffer = new List<Enemy>(64);
private Rigidbody2D _rb;

private void Awake()
{
    _rb = GetComponent<Rigidbody2D>();
}

private void Update()
{
    _enemyBuffer.Clear();  // riuso senza allocazione
    // usa _rb direttamente
}
```

- Usare il **Memory Profiler** e il **CPU Profiler** di Unity periodicamente per verificare assenza di GC alloc nei frame di gioco.
- Ogni PR che introduce allocazioni in hot-path rilevate dal profiler deve essere rifiutata e corretta.

---

## 5. Organizzazione del Codice C#

### Regola assoluta: tutto il codice C# vive dentro `Assets/Scripts/`

```
Assets/
└── Scripts/
    ├── Bootstrap/        # Inizializzazione, entry point (GameBootstrap.cs)
    ├── Core/             # Sistemi fondamentali (GameManager, EventBus, ecc.)
    ├── Entities/         # Logica di player, nemici, proiettili
    ├── Input/            # Wrapper e handler del New Input System
    ├── UI/               # Controller e presenter per l'interfaccia
    ├── Audio/            # AudioManager, sound pooling
    ├── Utilities/        # Classi helper, extension methods, pool generici
    └── Editor/           # Script solo-editor (devono stare in una cartella "Editor")
```

- **Nessun file `.cs` al di fuori di `Assets/Scripts/`** (eccetto codice auto-generato da Unity stesso che non va mai modificato manualmente).
- Namespace obbligatorio: ogni file deve dichiarare un namespace che rispecchia la struttura di cartelle, es. `namespace MobileGame.Core`, `namespace MobileGame.Input`.
- Una classe per file; il nome del file deve corrispondere esattamente al nome della classe.
- Tutti i campi privati usano il prefisso underscore + camelCase: `_myField`.
- Le costanti pubbliche usano `UPPER_SNAKE_CASE`; le proprietà pubbliche usano `PascalCase`.

---

## 6. Checklist Rapida per Ogni PR / Commit

- [ ] Nessun file `.meta` modificato, aggiunto manualmente o eliminato.
- [ ] Nessun riferimento a `UnityEngine.Input` (old input system).
- [ ] `Application.targetFrameRate = 60` presente nel bootstrap e non sovrascritto altrove.
- [ ] Nessuna allocazione heap (`new`, string concat, LINQ, boxing) in `Update` / `FixedUpdate` / loop ad alta frequenza.
- [ ] Tutti i file `.cs` risiedono sotto `Assets/Scripts/` con namespace corretto.
- [ ] Profiler run eseguita: GC Alloc = 0 B nei frame steady-state.
