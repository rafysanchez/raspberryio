﻿namespace Unosquare.RaspberryIO.Playground
{
    using Abstractions;
    using Camera;
    using Computer;
    using Peripherals;
    using Swan;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using WiringPi;

    /// <summary>
    /// Main entry point class.
    /// </summary>
    public static partial class Program
    {
        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
        /// <returns>A task representing the program.</returns>
        public static async Task Main()
        {
            $"Starting program at {DateTime.Now}".Info();

            Terminal.Settings.DisplayLoggingMessageType = LogMessageType.Info | LogMessageType.Warning | LogMessageType.Error;

            try
            {
                Pi.Init<BootstrapWiringPi>();

                // A set of very simple tests:
                await TestSystemInfo().ConfigureAwait(false);

                // TestCaptureImage();
                // TestCaptureVideo();

                // TestLedStripGraphics();
                // TestLedStrip();
                // TestRfidController();
                // TestLedBlinking();
                // TestHardwarePwm();
                // TestInfraredSensor();
                // TestServo();
                // await TestVolumeControl();
                // TestAccelerometer();
                // TestTempSensor();
                TestUltrasonicSensor();
            }
            catch (Exception ex)
            {
                "Error".Error(nameof(Main), ex);
            }
            finally
            {
                "Program finished.".Info();
                Terminal.Flush(TimeSpan.FromSeconds(2));
                if (Debugger.IsAttached)
                    "Press any key to exit . . .".ReadKey();
            }
        }

        /// <summary>
        /// Tests the ultrasonic sensor.
        /// </summary>
        public static void TestUltrasonicSensor()
        {
            using (var sensor = new UltrasonicHcsr04(Pi.Gpio[BcmPin.Gpio23], Pi.Gpio[BcmPin.Gpio24]))
            {
                sensor.OnDataAvailable += (s, e) =>
                {
                    if (e.IsValid)
                    {
                        if (e.HasObstacles)
                        {
                            if (e.Distance < 10)
                                $"Obstacle detected at {e.Distance:N2}cm / {e.DistanceInch:N2}in".WriteLine(ConsoleColor.Red);
                            else if (e.Distance < 30)
                                $"Obstacle detected at {e.Distance:N2}cm / {e.DistanceInch:N2}in".WriteLine(ConsoleColor.DarkYellow);
                            else if (e.Distance < 100)
                                $"Obstacle detected at {e.Distance:N2}cm / {e.DistanceInch:N2}in".WriteLine(ConsoleColor.Yellow);
                            else
                                $"Obstacle detected at {e.Distance:N2}cm / {e.DistanceInch:N2}in".WriteLine(ConsoleColor.Green);
                        }
                        else
                        {
                            "No obstacles detected.".Info("HC - SR04");
                        }
                    }
                    else
                    {
                        "Invalid Reading".Error("HC-SR04");
                    }
                };

                sensor.Start();
                Console.ReadKey(true);
            }
        }

        /// <summary>
        /// Tests the temperature sensor.
        /// </summary>
        public static void TestTempSensor()
        {
            using (var sensor = DhtSensor.Create(DhtType.Dht11, Pi.Gpio[BcmPin.Gpio04]))
            {
                var totalReadings = 0.0;
                var validReadings = 0.0;

                sensor.OnDataAvailable += (s, e) =>
                {
                    totalReadings++;
                    if (e.IsValid)
                    {
                        validReadings++;
                        $"Temperature: {e?.Temperature ?? 0:0.00}°C / {e?.TemperatureFahrenheit ?? 0:0.00}°F | Humidity: {e?.HumidityPercentage ?? 0:P0}".Info("DHT11");
                    }
                    else
                    {
                        "Invalid Reading".Error("DHT11");
                    }

                    $"Valid reading percentage: {validReadings / totalReadings:P}".Info("DHT11");
                };

                sensor.Start();
                Console.ReadKey(true);
            }
        }

        /// <summary>
        /// Test the GY521 Accelerometer and Gyroscope.
        /// </summary>
        public static void TestAccelerometer()
        {
            // Add device
            var accel_device = Pi.I2C.AddDevice(0x68);

            // Set accelerometer
            using (var accelSensor = new AccelerometerGY521(accel_device))
            {
                // Present info to screen
                accelSensor.DataAvailable +=
                    (s, e) => $"\nAccelerometer:\n{e.Accel}\n\nGyroscope:\n{e.Gyro}\n\nTemperature: {Math.Round(e.Temperature, 2)}°C\n".Info("GY-521");

                // Run accelerometer
                accelSensor.Start();
                Console.ReadKey(true);
            }
        }

        /// <summary>
        /// Tests the servo.
        /// </summary>
        // public static void TestServo()
        // {
        //    var servo = new HardwareServo((GpioPin)Pi.Gpio[BcmPin.Gpio18]);
        //    const double minPulse = 0.565;
        //    const double maxPulse = 2.620;
        //    var deltaPulse = 0.005;
        //
        //    while (true)
        //    {
        //        if (servo.PulseLengthMs >= maxPulse || servo.PulseLengthMs <= minPulse)
        //        {
        //            var stopPulseLength = servo.PulseLengthMs;
        //            while (true)
        //            {
        //                var k = "Q (increment), W (decrement) or E (scroll back)".ReadKey();
        //                if (k.Key == ConsoleKey.Q)
        //                {
        //                    servo.PulseLengthMs += Math.Abs(deltaPulse);
        //                    $"{servo}".Info("Servo");
        //                    var angle = servo.ComputeAngle(minPulse, maxPulse);
        //                    var pulseLength = servo.ComputePulseLength(angle, minPulse, maxPulse);
        //                    $"Angle is {angle,7:0.000}. Pulse Length Should be: {pulseLength,7:0.000}".Warn("Servo");
        //                }
        //                else if (k.Key == ConsoleKey.W)
        //                {
        //                    servo.PulseLengthMs -= Math.Abs(deltaPulse);
        //                    $"{servo}".Info("Servo");
        //                    var angle = servo.ComputeAngle(minPulse, maxPulse);
        //                    var pulseLength = servo.ComputePulseLength(angle, minPulse, maxPulse);
        //                    $"Angle is {angle,7:0.000}. Pulse Length Should be: {pulseLength,7:0.000}".Warn("Servo");
        //                }
        //                else if (k.Key == ConsoleKey.E)
        //                {
        //                    servo.PulseLengthMs = stopPulseLength;
        //                    $"{servo}".Info("Servo");
        //                    break;
        //                }
        //            }
        //
        //            deltaPulse *= -1;
        //            Thread.Sleep(100);
        //        }

        // servo.PulseLengthMs += deltaPulse;
        //        $"{servo} | Angle {servo.ComputeAngle(minPulse, maxPulse),7:0.00}".Info("Servo");
        //        Pi.Timing.SleepMicroseconds(1500);
        //    }
        // }
        
        /// <summary>
        /// Tests the infrared sensor HX1838.
        /// </summary>
        // public static void TestInfraredSensor()
        // {
        //    var inputPin = Pi.Gpio[BcmPin.Gpio23]; // BCM Pin 23 or Physical pin 16 on the right side of the header.
        //    var sensor = new InfraredSensor(inputPin, true);
        //    var emitter = new InfraredEmitter((GpioPin)Pi.Gpio[BcmPin.Gpio18]);
        //
        // sensor.DataAvailable += (s, e) =>
        //    {
        //        var necData = InfraredSensor.NecDecoder.DecodePulses(e.Pulses);
        //        if (necData != null)
        //        {
        //            $"NEC Data: {BitConverter.ToString(necData).Replace("-", " "),12}    Pulses: {e.Pulses.Length,4}    Duration(us): {e.TrainDurationUsecs,6}    Reason: {e.FlushReason}".Warn("IR");
        //
        //            if (InfraredSensor.NecDecoder.IsRepeatCode(e.Pulses))
        //                return;
        //
        //            // Test repeater signal
        //            var outputPulses = InfraredEmitter.NecEncoder.Encode(necData);
        //
        //            emitter.Send(outputPulses);
        //            var debugData = InfraredSensor.DebugPulses(outputPulses);
        //            $"TX       Length: {outputPulses.Length,5}".Warn("IR");
        //            debugData.Info("IR");
        //        }
        //        else
        //        {
        //            if (e.Pulses.Length >= 4)
        //            {
        //                var debugData = InfraredSensor.DebugPulses(e.Pulses);
        //                $"RX    Length: {e.Pulses.Length,5}; Duration: {e.TrainDurationUsecs,7}; Reason: {e.FlushReason}".Warn("IR");
        //                debugData.Info("IR");
        //            }
        //            else
        //            {
        //                $"RX (Garbage): {e.Pulses.Length,5}; Duration: {e.TrainDurationUsecs,7}; Reason: {e.FlushReason}".Error("IR");
        //            }
        //        }
        //    };
        //
        //    Console.ReadLine();
        //    sensor.Dispose();
        // }

        /// <summary>
        /// Tests the SPI bus functionality.
        /// </summary>
        public static void TestSpi()
        {
            Pi.Spi.Channel0Frequency = SpiChannel.MinFrequency;

            var request = Encoding.UTF8.GetBytes("Hello over SPI");
            $"SPI Request: {BitConverter.ToString(request)}".Info();
            var response = Pi.Spi.Channel0.SendReceive(request);
            $"SPI Response: {BitConverter.ToString(response)}".Info();

            $"SPI Base Stream Request: {BitConverter.ToString(request)}".Info();
            Pi.Spi.Channel0.Write(request);
            response = Pi.Spi.Channel0.SendReceive(new byte[request.Length]);
            $"SPI Base Stream Response: {BitConverter.ToString(response)}".Info();
        }

        /// <summary>
        /// Tests the display.
        /// </summary>
        public static void TestDisplay()
        {
            var input = string.Empty;

            while (input.Equals("x") == false)
            {
                "Enter brightness value (0 to 255). Enter b to toggle Backlight, Enter x to Exit".Info();
                input = Console.ReadLine();

                if (input?.Equals("b") == true)
                {
                    Pi.PiDisplay.IsBacklightOn = !Pi.PiDisplay.IsBacklightOn;
                }
                else if (byte.TryParse(input, out var value) && value != Pi.PiDisplay.Brightness)
                {
                    $"Current Value: {Pi.PiDisplay.Brightness}, New Value: {value}".Info();
                    Pi.PiDisplay.Brightness = value;
                }

                $"Display Status - Backlight: {Pi.PiDisplay.IsBacklightOn}, Brightness: {Pi.PiDisplay.Brightness}"
                    .Info();
            }

            Pi.PiDisplay.IsBacklightOn = true;
            Pi.PiDisplay.Brightness = 96;
            $"Display Status - Backlight: {Pi.PiDisplay.IsBacklightOn}, Brightness: {Pi.PiDisplay.Brightness}".Info();
        }

        /// <summary>
        /// Test volume control.
        /// </summary>
        /// <returns> Performs an audio task. </returns>
        public static async Task TestVolumeControl()
        {
            Console.WriteLine("Volume control for Pi - Playground");

            await Pi.Audio.SetVolumePercentage(85).ConfigureAwait(false);
            await Pi.Audio.SetVolumeByDecibels(-1.00f).ConfigureAwait(false);
            await Pi.Audio.IncrementVolume(4.00f).ConfigureAwait(false);
            
            await Pi.Audio.IncrementVolume(4.00f).ConfigureAwait(false);

            try
            {
                var currentState = await Pi.Audio.GetState(1, "PCM").ConfigureAwait(false);
                Console.WriteLine(currentState);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
            }

            try
            {
                var currentState = await Pi.Audio.GetState(0, "Master").ConfigureAwait(false);
                Console.WriteLine(currentState);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
            }

            try
            {
                await Pi.Audio.IncrementVolume(4.32f, 1, "PCM").ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
            }

            try
            {
                await Pi.Audio.IncrementVolume(2.06f, 0, "Master").ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Tests the LED blinking logic.
        /// </summary>
        public static void TestLedBlinking()
        {
            // Get a reference to the pin you need to use.
            // All methods below are exactly equivalent and reference the same pin
            var blinkingPin = Pi.Gpio[17];
            blinkingPin = Pi.Gpio[BcmPin.Gpio17];
            blinkingPin = Pi.Gpio[P1.Pin11];
            blinkingPin = ((GpioController)Pi.Gpio)[WiringPiPin.Pin00];
            blinkingPin = ((GpioController)Pi.Gpio).Pin17;
            blinkingPin = ((GpioController)Pi.Gpio).HeaderP1[11];

            // Configure the pin as an output
            blinkingPin.PinMode = GpioPinDriveMode.Output;

            // perform writes to the pin by toggling the isOn variable
            var isOn = false;
            for (var i = 0; i < 20; i++)
            {
                isOn = !isOn;
                blinkingPin.Write(isOn);
                Thread.Sleep(500);
            }
        }

        /// <summary>
        /// Tests the hardware PWM.
        /// </summary>
        public static void TestHardwarePwm()
        {
            // TODO: Check out:
            // https://raspberrypi.stackexchange.com/questions/4906/control-hardware-pwm-frequency
            // https://stackoverflow.com/questions/20081286/controlling-a-servo-with-raspberry-pi-using-the-hardware-pwm-with-wiringpi
            var pin = (GpioPin)Pi.Gpio[BcmPin.Gpio18];
            pin.PinMode = GpioPinDriveMode.PwmOutput;
            pin.PwmMode = PwmMode.MarkSign;
            pin.PwmClockDivisor = 3; // 1 is 4096, possible values are all powers of 2 starting from 2 to 2048
            pin.PwmRange = 800; // Range valid values I still need to investigate
            pin.PwmRegister = 600; // (int)(pin.PwmRange * 0.95); // This goes from 0 to 1024

            var probe = new LogicProbe(Pi.Gpio[P1.Pin11]);
            var probeBuffer = new List<LogicProbe.ProbeDataEventArgs>(1024);
            probe.ProbeDataAvailable += (s, e) =>
            {
                probeBuffer.Add(e);
                if (probeBuffer.Count >= 64)
                {
                    probe.Stop();
                }
            };

            probe.Start();
            while (probe.IsRunning)
                Thread.Sleep(15);

            Thread.Sleep(1000);
            foreach (var entry in probeBuffer)
            {
                Console.WriteLine(entry.ToString());
            }

            Console.WriteLine("finished");
        }

        private static async Task TestSystemInfo()
        {
            $"GPIO Controller initialized successfully with {Pi.Gpio.Count} pins".Info();
            $"{Pi.Info}".Info();
            try
            {
                $"BoardModel {Pi.Info.BoardModel}".Info();
                $"ProcessorModel {Pi.Info.ProcessorModel}".Info();
                $"Manufacturer {Pi.Info.Manufacturer}".Info();
                $"MemorySize {Pi.Info.MemorySize}".Info();
            }
            catch
            {
                // ignore
            }

            $"Uname {Pi.Info.OperatingSystem}".Info();
            $"HostName {NetworkSettings.Instance.HostName}".Info();
            $"Uptime (seconds) {Pi.Info.Uptime}".Info();
            var timeSpan = Pi.Info.UptimeTimeSpan;
            $"Uptime (timespan) {timeSpan.Days} days {timeSpan.Hours:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}"
                .Info();

            (await NetworkSettings.Instance.RetrieveAdapters())
                .Select(adapter =>
                    $"Adapter: {adapter.Name,6} | IPv4: {adapter.IPv4,16} | IPv6: {adapter.IPv6,28} | AP: {adapter.AccessPointName,16} | MAC: {adapter.MacAddress,18}")
                .ToList()
                .ForEach(x => x.Info());
        }

        private static void TestCaptureImage()
        {
            var pictureBytes = Pi.Camera.CaptureImageJpeg(640, 480);
            const string targetPath = "/home/pi/picture.jpg";

            if (File.Exists(targetPath))
                File.Delete(targetPath);

            File.WriteAllBytes(targetPath, pictureBytes);
            $"Took picture -- Byte count: {pictureBytes.Length}".Info();
        }

        private static void TestCaptureVideo()
        {
            // Setup our working variables
            var videoByteCount = 0;
            var videoEventCount = 0;
            var startTime = DateTime.UtcNow;

            // Configure video settings
            var videoSettings = new CameraVideoSettings
            {
                CaptureTimeoutMilliseconds = 0,
                CaptureDisplayPreview = false,
                ImageFlipVertically = true,
                CaptureExposure = CameraExposureMode.Night,
                CaptureWidth = 1920,
                CaptureHeight = 1080
            };

            try
            {
                "Press any key to START reading the video stream . . .".Info();
                Console.ReadLine();

                // Start the video recording
                Pi.Camera.OpenVideoStream(videoSettings,
                    onDataCallback: data =>
                    {
                        videoByteCount += data.Length;
                        videoEventCount++;
                    },
                    onExitCallback: null);

                // Wait for user interaction
                startTime = DateTime.UtcNow;
                "Press any key to STOP reading the video stream . . .".Info();
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                $"{ex.GetType()}: {ex.Message}".Error();
            }
            finally
            {
                // Always close the video stream to ensure raspivid quits
                Pi.Camera.CloseVideoStream();

                // Output the stats
                var megaBytesReceived = (videoByteCount / (1024f * 1024f)).ToString("0.000");
                var recordedSeconds = DateTime.UtcNow.Subtract(startTime).TotalSeconds.ToString("0.000");
                $"Capture Stopped. Received {megaBytesReceived} Mbytes in {videoEventCount} callbacks in {recordedSeconds} seconds"
                    .Info();
            }
        }
    }
}
