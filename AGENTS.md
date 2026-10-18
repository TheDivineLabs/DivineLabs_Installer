# DivineLabs Installer

On the owner's workstation keep source/caches under W:\DEV; canonical repository:
W:\DEV\projects\divinelabs\DivineLabs_Installer. Scratch: W:\DEV\.cache\temp;
retained artifacts: W:\DEV\artifacts. Other platforms/hosted CI use their workspace.

Keep Core portable with the public IInstallerEngine API and no UI dependencies. System APIs belong in Platforms.
Components contains reusable Avalonia controls without engine/platform/template dependencies.
Templates.Classic consumes Core/Components; Desktop is a thin standalone host. Keep Classic styling scoped to its window.
New programs use the public config contract. Preserve the Idle Slayer layout and document intentional differences.
Use proportional checks; do not launch games or install into actual user application folders during tests.
Transaction edits need focused failure/recovery tests; UI edits need affected renders.
Do not claim an OS is verified until that OS's native CI build and smoke check pass.
