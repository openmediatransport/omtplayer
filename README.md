# Open Media Transport (OMT) Decoder for Raspberry Pi 5

**omtplayer** is a decoder for Raspbery Pi 5 that can display an OMT source via the HDMI port at up to 1080p60.

A built in web server runs on port 8080 by default allowing sources to be selected for display.

## Requirements

* Raspberry Pi 5 with default OS installed. 2GB memory option is fine.
* dotnet8
* Clang
* Git
* libomtnet
* libvmx

## Performance

A base model 2GB Raspberry Pi 5 can comfortably decode at up to 1080p60

## Video Formats

**omtplayer** will attempt to match the OMT source format to a format the connected display supports. 

Decoding will not proceed if it is unable to find an exact resolution match. (Standard resolutions of 1280x720 and 1920x1080 should work on most displays)

Frame rates are more flexible and the app will auto select 60hz if an exact match is not available. (Monitors generally support higher frame rates of 50, 59.94 and 60hz while TVs also support lower frame rates directly such as 25 and 29.97)

Interlaces sources will be displayed as progressive without any deinterlacing. This is due to the Linux DRM API limitations in detecting field order.

## Instructions

1. Ensure that the Raspberry Pi OS is configued to boot to Console instead of Desktop.

omtplayer outputs directly to the display which is not possible when the Desktop mode is running.

This can be changed by running:

```
sudo raspi-config
```

And selecting Console under Boot Options - Desktop / CLI

2. Install dotnet 8 on to device.

Instructions can be found here:
https://learn.microsoft.com/en-us/dotnet/iot/deployment

**Important:** The --channel parameter should be set to 8.0

3. Install Clang

```
sudo apt install clang
```

4. Copy source code for the following repositories into the home directory in a structure similar to the following:

```
~/libvmx
~/libomtnet
~/omtplayer
```

The easiest way to do this is to git clone these repositories to the home directory using the commands below:

```
cd ~/
git clone https://github.com/openmediatransport/libvmx
git clone https://github.com/openmediatransport/libomtnet
git clone https://github.com/openmediatransport/omtplayer
```

5. Build libvmx 

```
cd ~/libvmx/build
chmod 755 buildlinuxarm64.sh
./buildlinuxarm64.sh
```

6. Build libomtnet 

```
cd ~/libomtnet/build
chmod 755 buildall.sh
./buildall.sh
```

7. Build omtplayer

```
cd ~/omtplayer/build
chmod 755 buildlinuxarm64.sh
./buildlinuxarm64.sh
```

8. All files needed will now be in ~/omtplayer/build/arm64

9. Run ~/omtplayer/build/arm64/omtplayer to start the decoder.

```
~/omtplayer/build/arm64/omtplayer
```

10. **Important:** Make sure the HDMI display is connected to HDMI 1 on the Pi. This is the HDMI port directly next to the USB-C power port.

11. Open a browser on another computer on the same network and connect to the web server to configure a source to connect to

```
http://piipaddress:8080/
```

12. omtplayer will remember the last selected source for future sessions automatically.

## Install as a service (optional)

This configures the app to run automatically when the device starts up.

1. Copy the omtplayer files from ~/omtplayer/build/arm64 into a folder called /opt/omtplayer on the system.

```
sudo mkdir /opt/omtplayer
sudo cp ~/omtplayer/build/arm64/* /opt/omtplayer/
```

2. Copy the omtplayer.service template into the /etc/systemd/system/ folder.

```
sudo cp ~/omtplayer/omtplayer.service /etc/systemd/system/
```

3. Reload systemctl and enable the service

```
sudo systemctl daemon-reload
sudo systemctl enable omtplayer
```

4. Start the service and check its status

```
sudo systemctl start omtplayer
sudo systemctl status omtplayer
```

If successful, the web server should now be accessible on port 8080