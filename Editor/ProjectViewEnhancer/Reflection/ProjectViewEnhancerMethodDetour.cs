using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JLZ.Editor.ProjectViewEnhancer
{
    internal sealed class ProjectViewEnhancerMethodDetour
    {
        private const uint PageExecuteReadWrite = 0x40;
        private const int X64JumpSize = 12;

        private readonly MethodInfo _targetMethod;
        private readonly MethodInfo _replacementMethod;

        private IntPtr _targetAddress = IntPtr.Zero;
        private byte[] _originalBytes;

        public ProjectViewEnhancerMethodDetour(MethodInfo targetMethod, MethodInfo replacementMethod)
        {
            _targetMethod = targetMethod;
            _replacementMethod = replacementMethod;
        }

        public bool IsInstalled { get; private set; }

        public void Install()
        {
            if (IsInstalled)
                return;

            if (_targetMethod == null || _replacementMethod == null)
                throw new InvalidOperationException("Target or replacement method is missing.");

            RuntimeHelpers.PrepareMethod(_targetMethod.MethodHandle);
            RuntimeHelpers.PrepareMethod(_replacementMethod.MethodHandle);

            _targetAddress = _targetMethod.MethodHandle.GetFunctionPointer();
            IntPtr replacementAddress = _replacementMethod.MethodHandle.GetFunctionPointer();

            _originalBytes = new byte[X64JumpSize];
            Marshal.Copy(_targetAddress, _originalBytes, 0, _originalBytes.Length);

            WriteJump(_targetAddress, replacementAddress);
            IsInstalled = true;
        }

        public void Uninstall()
        {
            if (!IsInstalled || _targetAddress == IntPtr.Zero || _originalBytes == null || _originalBytes.Length == 0)
                return;

            WriteBytes(_targetAddress, _originalBytes);

            _targetAddress = IntPtr.Zero;
            _originalBytes = null;
            IsInstalled = false;
        }

        private static void WriteJump(IntPtr sourceAddress, IntPtr destinationAddress)
        {
            byte[] jumpBytes = new byte[X64JumpSize];
            jumpBytes[0] = 0x48;
            jumpBytes[1] = 0xB8;

            byte[] destinationBytes = BitConverter.GetBytes(destinationAddress.ToInt64());
            Buffer.BlockCopy(destinationBytes, 0, jumpBytes, 2, destinationBytes.Length);

            jumpBytes[10] = 0xFF;
            jumpBytes[11] = 0xE0;

            WriteBytes(sourceAddress, jumpBytes);
        }

        private static void WriteBytes(IntPtr address, byte[] bytes)
        {
            if (!VirtualProtect(address, new UIntPtr((uint)bytes.Length), PageExecuteReadWrite, out uint oldProtect))
            {
                throw new InvalidOperationException($"VirtualProtect failed with error code {Marshal.GetLastWin32Error()}.");
            }

            try
            {
                Marshal.Copy(bytes, 0, address, bytes.Length);
                FlushInstructionCache(GetCurrentProcess(), address, new UIntPtr((uint)bytes.Length));
            }
            finally
            {
                VirtualProtect(address, new UIntPtr((uint)bytes.Length), oldProtect, out _);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(
            IntPtr lpAddress,
            UIntPtr dwSize,
            uint flNewProtect,
            out uint lpflOldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushInstructionCache(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            UIntPtr dwSize);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();
    }
}
