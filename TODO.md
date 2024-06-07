### TODO
- Add wiki with mouse control info https://forum.kerbalspaceprogram.com/index.php?/topic/201063-camera-tools-continued-v1150/&do=findComment&comment=4077185
- Allow world-based coordinates for the pathing camera
	- Geo-spatial pathing mode for pathing between GPS coordinates
- Chase-plane option in dogfight mode to have the camera point at the vessel as if from a chase-plane (disable roll).
	- Option to pick the part to follow instead of CoM / cockpit.
- Adjust the inertial mode to allow larger initial lags due to sharp movements without becoming unstable.
- Add inertia to the roll in inertial mode.
- Ping "HB Stratos" on Scott's discord when done.
- Add controller support.
	- Buttons should already be mappable as keybinds, but it would be better to have controller buttons in addition to keyboard keys.
	- Input.GetAxis should be able to grab the axes (use velocity mode for these).

### Bug
- "I also just noticed a bit of a weird effect in the most recent camera tools version that I don't recall noticing last time. In inertia camera with roll set to zero, the camera still slowly and sluggishly rolls, and not only when the plane passes through the vertical axis, but when flying a normal level aileron roll."

### Ideas
- Add the ability to use the stationary and pathing modes in the Editor.
