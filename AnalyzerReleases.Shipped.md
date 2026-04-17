## Release 0.3.0

### New Rules
- **SK003**: Service attribute declares unimplemented type
  _Category:_ ServiceKit.Usage • _Severity:_ Error
  _Triggers on:_ `[Service(typeof(IFoo))]` on a class that does not implement `IFoo`.
  _Code fix:_ Add the missing interface to the class's base list.

- **SK005**: ServiceKitBehaviour.Awake override must call base.Awake()
  _Category:_ ServiceKit.Usage • _Severity:_ Error
  _Triggers on:_ A `ServiceKitBehaviour` subclass that overrides `Awake()` without calling `base.Awake()`.
  _Code fix:_ Insert `base.Awake();` as the first statement in the method body.

---

## Release 0.2.0
### New Rules
- **SK002**: Injected field should be private, non-static, non-readonly, and not `[SerializeField]`  
  _Category:_ ServiceKit.Usage • _Severity:_ Warning  
  _Code fix:_ Make field `private`, remove `static`/`readonly`, and remove `[SerializeField]`.

- **SK004**: Injection chain must specify cancellation  
  _Category:_ ServiceKit.Async • _Severity:_ Warning  
  _Triggers on:_ `InjectServicesAsync(...).ExecuteAsync()` without `.WithCancellation(...)`.  
  _Code fixes:_ Add `.WithCancellation(destroyCancellationToken)` **or** replace with `.ExecuteWithCancellationAsync(destroyCancellationToken)`.

- **SK010**: Prefer `ExecuteWithCancellationAsync(token)` over `.WithCancellation(token).ExecuteAsync()`  
  _Category:_ ServiceKit.Async • _Severity:_ Info  
  _Code fix:_ Convert chain to `.ExecuteWithCancellationAsync(token)` and remove `.WithCancellation(token)`.

### Changed Rules
- **SK001**: Injected member should be an interface  
  Expanded to analyze properties in addition to fields, improved attribute detection and diagnostics location, and added a code fix to switch the declaration type to an implemented interface.

---

## Release 0.1.0
### New Rules
- **SK001**: Injected member should be an interface
