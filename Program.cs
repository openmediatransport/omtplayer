/*
* MIT License
*
* Copyright (c) 2025 Open Media Transport Contributors
*
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
*
* The above copyright notice and this permission notice shall be included in all
* copies or substantial portions of the Software.
*
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
* SOFTWARE.
*
*/

using libomtnet;
using omtplayer.drm;
using omtplayer.web;

namespace omtplayer
{
    internal class Program
    {
        private static WebServer? server = null;
        private static bool running = true;

        static void WriteLog(string message)
        {
            if (server != null)
            {
                server.WriteLog(message);
            }
            Console.WriteLine(message);
        }

        static void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("Closing...");
            running = false;
            e.Cancel = true;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("OMT Player");
            try
            {
                server = new WebServer();
                Console.CancelKeyPress += Console_CancelKeyPress;

                string devicePath = "/dev/dri/card1";
                DRMDevice? dev = null;
                dev = new DRMDevice(devicePath);
                WriteLog("DisplayDevice.Opened: " + devicePath);
                DRMConnector? connector = null;
                while (connector == null)
                {
                    connector = dev.GetFirstActiveConnector();
                    if (connector == null)
                    {
                        WriteLog("DisplayDevice.WaitingForConnection");
                        Thread.Sleep(2000);
                        dev.Dispose();
                        dev = new DRMDevice(devicePath);
                    }
                    if (!running) break;
                }
                if (connector != null)
                {

                    dev.StartEvents();

                    WriteLog("Display.Formats:");
                    DRMMode[] modes = connector.Modes;
                    if (modes != null)
                    {
                        foreach (DRMMode mode in modes)
                        {
                            string? s = mode.ToString();
                            if (s != null)
                            {
                                WriteLog(s);
                            }
                        }
                    }

                    DRMPresenter? presenter = null;
                    int currentWidth = 0;
                    int currentHeight = 0;
                    bool currentInterlaced = false;
                    float currentFrameRate = 0;
                    string currentSource = server.Source;

                    OMTReceive r = new OMTReceive(currentSource, OMTFrameType.Video, OMTPreferredVideoFormat.BGRA, OMTReceiveFlags.None);
                    OMTMediaFrame frame = new OMTMediaFrame();
                    while (running)
                    {
                        if (currentSource != server.Source)
                        {
                            WriteLog("Source.Changed: " + server.Source);
                            r.Dispose();
                            currentSource = server.Source;
                            r = new OMTReceive(currentSource, OMTFrameType.Video, OMTPreferredVideoFormat.BGRA, OMTReceiveFlags.None);
                        }
                        if (r.Receive(OMTFrameType.Video, 500, ref frame))
                        {
                            bool interlaced = false;
                            if (frame.Flags.HasFlag(OMTVideoFlags.Interlaced)) interlaced = true;
                            if (currentWidth != frame.Width || currentHeight != frame.Height || currentFrameRate != frame.FrameRate || currentInterlaced != interlaced)
                            {
                                currentWidth = frame.Width;
                                currentHeight = frame.Height;
                                currentFrameRate = frame.FrameRate;
                                currentInterlaced = interlaced;
                                if (presenter != null)
                                {
                                    dev.SetPresenter(null);
                                    presenter.Dispose();
                                    presenter = null;
                                    WriteLog("Presenter.Clear");
                                }
                                WriteLog("Receive.NewFormat: " + frame.Width + "x" + frame.Height + " " + frame.FrameRate.ToString());
                                DRMMode? mode = connector.FindNearestMode(frame.Width, frame.Height, frame.FrameRate, false);
                                if (mode != null)
                                {
                                    WriteLog("Presenter.NearestMatch: " + mode.ToString());
                                    presenter = new DRMPresenter(dev, connector, mode, 3);
                                    dev.SetPresenter(presenter);
                                    WriteLog("Presenter.Created");
                                }
                                else
                                {
                                    WriteLog("Presenter.NoDisplayModesFound");
                                }
                            }
                            if (presenter != null)
                            {
                                presenter.Enqueue(frame.Data, frame.Stride);
                            }
                        }
                        else
                        {
                            WriteLog("Receive.NoFrame");
                        }
                    }
                    if (r != null)
                    {
                        r.Dispose();
                    }
                    if (presenter != null)
                    {
                        presenter.Dispose();
                    }
                }
                if (dev != null)
                {
                    dev.Dispose();
                }
                if (server != null)
                {
                    server.StopServer();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

    }
}
