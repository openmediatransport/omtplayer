# Open Media Transport (OMT) Decoder for Raspberry Pi 5

**omtplayer** is a decoder for Raspbery Pi 5 that can display an OMT source via the HDMI port at up to 1080p60.

A built in web server runs on port 8080 by default allowing sources to be selected for display.

## Requirements

* Raspberry Pi 5 with default OS installed. 2GB memory option is fine.
* dotnet8
* Clang
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

1. Install dotnet 8 on to device.

Instructions can be found here:
https://learn.microsoft.com/en-us/dotnet/iot/deployment

**Important:** The --channel parameter should be set to 8.0

2. Install Clang

```
sudo apt install clang
```

3. Copy source code for the following repositories into a folder structure similar to the following:

```
/libvmx
/libomtnet
/omtplayer
```

4. Build libvmx by running /libvmx/build/buildlinuxarm64.sh

5. Build libomtnet by running /libomtnet/build/buildall.sh

6. Build omtplayer by running /omtplayer/build/buildlinuxarm64.sh

7. All files needed will now be in /omtplayer/build/arm64

8. Run /omtplayer/build/arm64/omtplayer to start the decoder.

9. Open a browser on another computer on the same network and connect to the web server to configure a source to connect to

```
http://piipaddress:8080/
```

10. omtplayer will remember the last selected source for future sessions automatically.

## Install as a service (optional)

This configures the app to run automatically when the device starts up.

1. Copy the omtplayer files into a folder called /opt/omtplayer on the system.
2. Copy the omtplayer.service template into the /etc/systemd/system/ folder.
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