# Rooster Mod Manager - Comprehensive Audit

## 1. Executive Summary
**Rooster** is a functional, lightweight mod manager integrated directly into Ultimate Chicken Horse. It successfully handles the core lifecycle of mod auto-discovery and updating. Its strongest points are its robust installation heuristics (handling various ZIP structures) and its "Update Loop Prevention" self-healing mechanism.

**Critical Gaps:**
- **Reliability:** No Dependency Resolution. Updating a mod that introduces a new dependency will result in a broken state.
- **Architecture:** `UpdateChecker` acts as a "God Class", coupling logic with UI orchestration.

---

## 2. Architecture & Code Quality

### 2.1 Project Structure
The project is well-structured with clear separation of concerns in folders:
- `Services/`: Core logic (Network, File I/O, Matching).
- `UI/`: Interface management (reusing Game's Tablet UI).
- `Patches/`: Game hooks.
- `Models/`: Data structures.

**Rating:** ⭐⭐⭐⭐☆ (Very Good)

### 2.2 Code Patterns
- **Service Isolation:** Usage of static service classes (`ModMatcher`, `UpdateInstaller`) is good for testability and portability.
- **Async Handling:** Heavy reliance on Unity Coroutines. While functional, this ties logic to the Unity lifecycle and makes unit testing logic harder without a test runner.
- **Performance:** Manual JSON parsing in `ThunderstoreApi` is a smart optimization to reduce GC pressure when fetching the massive mod list.
- **Error Handling:** pervasive `try-catch` blocks prevent game crashes, which is appropriate for a mod, though it necessitates careful log monitoring to debug issues.

### 2.3 Key Classes Review
- **`UpdateChecker.cs`**: Violates Single Responsibility Principle. It handles:
    1. Fetching data (Network)
    2. Orchestrating matching (Logic)
    3. Scheduling updates (Logic)
    4. Triggering UI notifications (Presentation)
    *Recommendation:* Split into `UpdateService` (logic only) and `UpdateManager` (orchestrator).
- **`ModMatcher.cs`**: Excellent implementation of fuzzy matching. Pure logic, easy to test.
- **`UpdateLoopPreventer.cs`**: A critical stability feature that prevents the game from getting stuck in a "boot-crash-reinstall" loop.

---

## 3. Feature Audit

| Feature | Status | Notes |
| :--- | :--- | :--- |
| **Auto-Discovery** | ✅ Implemented | Uses smart heuristics (Name, GUID, Tokens). |
| **Update Checking** | ✅ Implemented | Checks against Thunderstore API. |
| **Installation** | ✅ Implemented | Robust. Handles `BepInEx/`, `plugins/`, `patchers/`, and flat structures. |
| **Hot Swapping** | ✅ Implemented | Uses `.old` file renaming to safe-swap DLLs at runtime (applied on restart). |
| **Self-Healing** | ✅ Implemented | `UpdateLoopPreventer` detects and disables broken updates. |
| **Manual Configuration** | ⚠️ Partial | Can toggle Auto-Update/Ignore, but cannot manually fix Mismatched mods. |
| **Dependency Resolution** | ❌ Missing | **Critical.** Does not check or install mod dependencies. |
| **Hash Verification** | ⚠️ N/A | **Platform Limitation.** Thunderstore API v1 does not provide file hashes. |
| **Uninstallation** | ❌ Missing | Users must manually delete files. |
| **Downgrading** | ❌ Missing | Only verifies against the `latest` version. |

---

## 4. UI/UX Review

- **Integration:** Cleverly hooks into the native `TabletModalOverlay`, making it feel like part of the game.
- **Responsiveness:** Good use of async calls prevents UI freezes.
- **Visuals:** Functional, utilizing a "White Sprite" system for clean flat UI.
- **Feedback:** "Checking for Mod Updates..." notification is clear.
- **Issues:**
    - Repurposing the Tablet UI is fragile; changes to the base game's UI Code could break the entire manager.

---

## 5. Security & Privacy
- **Transport:** HTTPS is used for all requests.
- **Privacy:** One-way communication (GET requests only). No telemetry sent.
- **Integrity:** The mod relies on HTTPS transport security as the Thunderstore API does not currently expose file hashes for verification.

---

## 6. Recommendations & Roadmap

### Priority 1: Critical Fixes
1.  **Implement Dependency Resolution:**
    - Parse `dependencies` array from Thunderstore API.
    - When updating `Mod A`, recursively check if `Dependency B` is installed and meets version requirements. Queue it for download if missing.

### Priority 2: Refactoring
1.  **Decouple `UpdateChecker`:** Extract the "Notification/UI" logic into a separate `UpdateNotificationService`.
2.  **Unit Tests:** Create a distinct Test Assembly. `ModMatcher` and `VersionComparer` are prime candidates for logic tests.

### Priority 3: Features
1.  **Manual Match Override:** Allow users to manually link a local DLL to a Thunderstore ID in the UI if the heuristic fails.
2.  **Uninstaller:** Add a "Delete" button in the Mod Menu.
