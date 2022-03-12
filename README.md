# ThrottleOverlay #

### *Simple overlay widget to visualize your joystick's throttle position* ###

Use this tool to keep positional awareness of your throttle axis -- this is especially useful if you have a joystick with a dodgy/crappy throttle slider, like mine (or maybe a nicer throttle that slides around your desk and isn't fixed, relative to your seat).

## Change Log ##

#### v1.0:

- Initial release.

## Instructions ##

#### *Download and Install:*

- https://github.com/arithex/ThrottleOverlay/releases 
- Simply download and unzip, anywhere you like.  No installer, no special dependencies, and no special permissions required.
- Requires .NET Framework 4.8 (should run ok on Windows 10 version 1903 and later).
- x64 only.

#### *Configuration:*

Configure the ProductID, VendorID, and axis ID for your throttle in the `ThrottleOverlay.exe.config` file.

- To lookup your pid/vid code, consult the `DeviceSorting.txt` or `DeviceDefaults.txt` files in your `\Falcon BMS 4.35\User\Config` directory.
  - The PID is the first 4 hex character codes of the GUID; the VID is the next 4 hex character codes.
  - VKB's JoyTester app (https://vkbcontrollers.com/?faq=how-does-vkbjoytester-work) also conveniently shows your PID and VID.
- The most common axis IDs, for joystick throttles, are:
  - Z => 50
  - RZ => 53
  - Slider0 => 54

- (optional) Adjust the position, width and height of the bar. Tip: use negative values to offset from the right/bottom of screen.

#### *Pre-flight:*

- Falcon BMS: on the Controller Setup page, calibrate the afterburner detent -- move the throttle to one tick _below_ yellow (ie. maximum bright-green) and click the 'set AB detent' button.

## Future

- Auto-configuration / device detection

- Support for dual throttle axes

- Different UX templates (eg. analog dial)
