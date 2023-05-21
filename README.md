# FirstPersonTemplate

---- Install -----

Connect included assembly to BML.scripts, SO system, etc

Use the included first person game input actions
For both player input component and also any UI event systems

Copy scriptable objects from FirstPersonTemplate to the project's main repo and connect them FirstPersonTemplate components
Make sure SO for settings (Ex. setting_FOV) are connected

Make sure proper layers exist:
- Interactable
- PlayerAlwaysOnTop
- Player

Set desired font for hover text

Remember theres 2 cameras so any new layers need to be added manually
Would also need to add post processing effects to both cameras

To use 2D weapon, enable 2D Weapon Pivot under player world space UI Canvas

---- Dependencies -----
- BML SO System
- World Space UI
- Cinemachine
- Kinematic Character Controller
- FEEL
- Odin Inspector