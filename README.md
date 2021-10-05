# ThrottleOverlay #

### *Simple WPF app to easily visualize your joystick/throttle position* ###

Use this tool to keep visual tabs on your throttle axis -- this is especially useful, if you have a joystick with a dodgy/crappy throttle slider like mine (or a nicer throttle that slides around your desk and isn't fixed relative to your seat).

## Change Log ##

#### v1.0:

- Initial release.

## Instructions ##

#### *Download and Install:*

- https://github.com/arithex/ThrottleOverlay/releases 
- Simply download and unzip, anywhere you like.  No installer, no special dependencies, and no special permissions required.
- Requires .NET Framework 4.8 (should run ok on Windows 10 version 1903 and later).
- x64 only.

#### *Configuration settings in `ThrottleOverlay.exe.config` file:*

- Configure the pid/vid and axis-id, for your throttle; and axis-reversal, if necessary.
  - To lookup your pid/vid code, consult the `DeviceSorting.txt` or `DeviceDefaults.txt` files in your `\Falcon BMS 4.35\User\Config` directory.
    - The PID is the first 4 hex character codes of the GUID; the VID is the next 4 hex character codes.
    - VKB's JoyTester app (https://vkbcontrollers.com/?faq=how-does-vkbjoytester-work) also conveniently shows your PID and VID.
  - Most common axis IDs:
    - X => 48
    - Y => 49
    - Z => 50
    - RZ => 53
    - Slider0 => 54

- (optional) Adjust the position, width and height of the bar. Tip: negative values offset from the right/bottom of screen.

#### *Pre-flight:*

- Falcon BMS: to calibrate the afterburner detent -- simply move the throttle to one tick below yellow (maximum bright-green) and click the 'set AB detent' button, on the Controller Setup page.

## Future

- Customizable colors

- Support for dual/multiple throttles

- Different UX templates (eg. analog dial?)

- Sound effect, passing through AB detent
