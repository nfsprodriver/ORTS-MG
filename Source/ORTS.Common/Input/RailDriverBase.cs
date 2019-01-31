﻿using System;

namespace ORTS.Common.Input
{
    public abstract class RailDriverBase
    {
        public abstract int WriteBufferSize { get; }

        public abstract int ReadBufferSize { get; }

        public abstract int WriteData(byte[] writeBuffer);

        public byte[] NewReadBuffer => new byte[ReadBufferSize]; 

        public byte[] NewWriteBuffer => new byte[WriteBufferSize];

        public abstract int ReadCurrentData(ref byte[] data);

        public abstract int BlockingReadCurrentData(ref byte[] data, int timeout);

        public abstract void Shutdown();

        public abstract bool Enabled { get; }

        private static RailDriverBase instance;
        public static RailDriverBase GetInstance()
        {
            if (null == instance)
            {
                if (Environment.Is64BitProcess)
                {
                    instance = new RailDriver64();
                }
                else
                {
                    instance= new RailDriver32();
                }
            }
            return instance;
        }
    }

    internal class RailDriver32 : RailDriverBase
    {
        private readonly PIEHid32Net.PIEDevice device;                   // Our RailDriver

        public RailDriver32()
        {
            try
            {
                foreach (PIEHid32Net.PIEDevice currentDevice in PIEHid32Net.PIEDevice.EnumeratePIE())
                {
                    if (currentDevice.HidUsagePage == 0xc && currentDevice.Pid == 210)
                    {
                        device = currentDevice;
                        device.SetupInterface();
                        device.suppressDuplicateReports = true;
                        break;
                    }
                }
            }
            catch (Exception error)
            {
                device = null;
                System.Diagnostics.Trace.WriteLine(error);
            }
        }

        public override int WriteBufferSize => device?.WriteLength ?? 0;

        public override int ReadBufferSize => device?.ReadLength ?? 0;

        public override bool Enabled => device != null;

        public override int BlockingReadCurrentData(ref byte[] data, int timeout)
        {
            return device?.BlockingReadData(ref data, timeout) ?? -1;
        }

        public override int ReadCurrentData(ref byte[] data)
        {
            return device?.ReadLast(ref data) ?? -1;
        }

        public override void Shutdown()
        {
            device?.CloseInterface();
        }

        public override int WriteData(byte[] writeBuffer)
        {
            return device?.WriteData(writeBuffer) ?? -1;
        }
    }

    internal class RailDriver64 : RailDriverBase
    {
        private readonly PIEHid64Net.PIEDevice device;                   // Our RailDriver

        public RailDriver64()
        {
            try
            {
                foreach (PIEHid64Net.PIEDevice currentDevice in PIEHid64Net.PIEDevice.EnumeratePIE())
                {
                    if (currentDevice.HidUsagePage == 0xc && currentDevice.Pid == 210)
                    {
                        device = currentDevice;
                        device.SetupInterface();
                        device.suppressDuplicateReports = true;
                        break;
                    }
                }
            }
            catch (Exception error)
            {
                device = null;
                System.Diagnostics.Trace.WriteLine(error);
            }
        }

        public override int WriteBufferSize => (int)(device?.WriteLength ?? 0);

        public override int ReadBufferSize => (int)(device?.ReadLength ?? 0);

        public override bool Enabled => device != null;

        public override int BlockingReadCurrentData(ref byte[] data, int timeout)
        {
            return device?.BlockingReadData(ref data, timeout) ?? -1;
        }

        public override int ReadCurrentData(ref byte[] data)
        {
            return device?.ReadLast(ref data) ?? -1;
        }

        public override void Shutdown()
        {
            device?.CloseInterface();
        }

        public override int WriteData(byte[] writeBuffer)
        {
            return device?.WriteData(writeBuffer) ?? -1;
        }
    }

}
