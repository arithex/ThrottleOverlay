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

- (optional) Adjust the position, width and height of the bar. Tip: negative values offset from the right/bottom of screen.

#### *Pre-flight:*

- Falcon BMS: to calibrate the afterburner detent -- simply move the throttle to one tick below yellow (bright green) and click the 'set AB detent' button on the Controller Setup page.

## Future

- Support for dual/multiple throttles?

- Different UX templates (eg. analog dial)

- Configurable sound effects (eg. passing through AB detent)
