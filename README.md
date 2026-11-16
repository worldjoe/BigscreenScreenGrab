# Bigscreen Screen Grab

Experimental MelonLoader mod for moving and resizing Bigscreen's main
presentation screen. It also provides **Cam 2 follow**, which frames the main
screen for the desktop feed used by streaming applications.
This is an unofficial community mod, not affiliated with Bigscreen, Inc.

## Requirements

- Windows, Bigscreen PC VR, and IL2CPP-compatible MelonLoader. Launch the
  game once with MelonLoader to generate `MelonLoader\Il2CppAssemblies`.
- BigscreenBehind for the **Screen Grab** and **Cam 2 follow** items in its
  Mod Selection menu.
- VR controllers for grip and joystick controls.
- .NET 6 SDK or a later SDK capable of targeting .NET 6, for building.

Developed against Bigscreen `0.950.2.03f010-beta-perma-rooms-beta`,
MelonLoader `0.7.1` open beta, and Unity `2020.3.33f1`. Game updates may
change the screen, camera, or menu APIs this mod uses.

## Build

From this directory in PowerShell:

```powershell
dotnet build .\BigscreenScreenGrab.csproj -c Release
```

The default Bigscreen path is
`C:\Program Files (x86)\Steam\steamapps\common\Bigscreen`. Override it if
needed:

```powershell
dotnet build .\BigscreenScreenGrab.csproj -c Release "-p:GameDir=D:\SteamLibrary\steamapps\common\Bigscreen"
```

Output: `bin\Release\net6.0\BigscreenScreenGrab.dll`. The build references
assemblies in the installed game and MelonLoader directories.

## Install and use

1. Close Bigscreen and copy `BigscreenScreenGrab.dll` into its `Mods` folder.
2. Launch Bigscreen and enter a room with a main presentation screen.
3. Point at the main screen and hold **grip** to move it. The controller's
   Bigscreen pointer must hit the main screen, so a personal screen in front
   will not also move the main screen. The screen turns toward the headset
   and uses the headset's up direction for orientation.
4. While holding grip, push the **same controller's joystick up or down** to
   enlarge or shrink the screen proportionally around its center. Release
   grip when finished. Teleport controls remain disabled until that joystick
   returns to neutral, preventing accidental teleportation during resizing.
5. Bigscreen's target-shaped recenter button also places the screen in front
   of the headset at its current distance, aligned with the headset view.

In **Bigscreen Behind > Mod Selection**, **Screen Grab** enables or disables
movement, resizing, and screen recentering. It defaults on and remembers the
selection. Turning it off restores the position, rotation, and size captured
before the mod first changed that screen. Modded placement is not saved across
sessions. Bigscreen's normal recenter action remains in place.

## Cam 2 follow and Discord

**Cam 2 follow** is an independent Mod Selection toggle, enabled by default
and remembered. When enabled, it attempts to start Cam 2, point it at the
main screen, and hide its camera model. It frames the whole screen; black
bars may appear when aspect ratios differ. One other player confirmed the
camera model was hidden in testing.

The mod isolates the screen's render layer and outputs black when another
visible renderer shares that layer or framing cannot be established. This
is a precaution, not a privacy guarantee: **inspect the Bigscreen desktop
feed before every Discord stream**, particularly after changing rooms,
screens, or game versions. If Cam 2 does not start, open Bigscreen's streamer
camera controls and inspect `MelonLoader\Latest.log`.

Turning follow off restores captured camera settings and position. If the
mod started Cam 2, it requests that Cam 2 be turned off. A camera that was
already on is left on, and its restored view may include personal monitors.

Screen movement changes the presentation screen root. Effects on other
players have not been independently verified; test in a private room before
using it in a shared room. Close Bigscreen and remove the DLL from `Mods` to
uninstall.
