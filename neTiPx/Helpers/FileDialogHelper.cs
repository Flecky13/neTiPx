using System;
using System.Runtime.InteropServices;
using System.Text;

namespace neTiPx.Helpers
{
    internal static class FileDialogHelper
    {
        private const int OFN_FILEMUSTEXIST = 0x00001000;
        private const int OFN_PATHMUSTEXIST = 0x00000800;
        private const int OFN_EXPLORER = 0x00080000;
        private const int OFN_HIDEREADONLY = 0x00000004;
        private const int OFN_OVERWRITEPROMPT = 0x00000002;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OPENFILENAMEW
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public IntPtr lpstrFilter;
            public IntPtr lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public IntPtr lpstrFileTitle;
            public int nMaxFileTitle;
            public IntPtr lpstrInitialDir;
            public IntPtr lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public IntPtr lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public IntPtr lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int FlagsEx;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetOpenFileName(ref OPENFILENAMEW ofn);

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetSaveFileName(ref OPENFILENAMEW ofn);

        public static bool TryOpenFile(IntPtr ownerHwnd, string title, string filter, out string selectedPath)
        {
            return TryRunDialog(
                ownerHwnd,
                title,
                filter,
                defaultExtension: string.Empty,
                suggestedFileName: string.Empty,
                addFlags: OFN_FILEMUSTEXIST,
                invokeDialog: GetOpenFileName,
                out selectedPath);
        }

        public static bool TrySaveFile(IntPtr ownerHwnd, string title, string filter, string defaultExtension, string suggestedFileName, out string selectedPath)
        {
            return TryRunDialog(
                ownerHwnd,
                title,
                filter,
                defaultExtension,
                suggestedFileName,
                OFN_OVERWRITEPROMPT,
                GetSaveFileName,
                out selectedPath);
        }

        private static bool TryRunDialog(
            IntPtr ownerHwnd,
            string title,
            string filter,
            string defaultExtension,
            string suggestedFileName,
            int addFlags,
            DialogInvoker invokeDialog,
            out string selectedPath)
        {
            const int fileBufferChars = 2048;

            IntPtr filterPtr = IntPtr.Zero;
            IntPtr titlePtr = IntPtr.Zero;
            IntPtr defExtPtr = IntPtr.Zero;
            IntPtr fileBufferPtr = IntPtr.Zero;

            try
            {
                filterPtr = Marshal.StringToHGlobalUni(filter ?? string.Empty);
                titlePtr = Marshal.StringToHGlobalUni(title ?? string.Empty);
                defExtPtr = Marshal.StringToHGlobalUni(defaultExtension ?? string.Empty);
                fileBufferPtr = Marshal.AllocHGlobal(fileBufferChars * sizeof(char));

                var emptyBuffer = new byte[fileBufferChars * sizeof(char)];
                Marshal.Copy(emptyBuffer, 0, fileBufferPtr, emptyBuffer.Length);

                if (!string.IsNullOrWhiteSpace(suggestedFileName))
                {
                    var chars = suggestedFileName.ToCharArray();
                    var copyLength = Math.Min(chars.Length, fileBufferChars - 1);
                    Marshal.Copy(chars, 0, fileBufferPtr, copyLength);
                    Marshal.WriteInt16(fileBufferPtr, copyLength * sizeof(char), 0);
                }

                var ofn = new OPENFILENAMEW
                {
                    lStructSize = Marshal.SizeOf<OPENFILENAMEW>(),
                    hwndOwner = ownerHwnd,
                    lpstrFilter = filterPtr,
                    nFilterIndex = 1,
                    lpstrFile = fileBufferPtr,
                    nMaxFile = fileBufferChars,
                    lpstrTitle = titlePtr,
                    lpstrDefExt = defExtPtr,
                    Flags = OFN_PATHMUSTEXIST | OFN_EXPLORER | OFN_HIDEREADONLY | addFlags
                };

                if (!invokeDialog(ref ofn))
                {
                    selectedPath = string.Empty;
                    return false;
                }

                selectedPath = Marshal.PtrToStringUni(ofn.lpstrFile) ?? string.Empty;
                return !string.IsNullOrWhiteSpace(selectedPath);
            }
            finally
            {
                if (filterPtr != IntPtr.Zero) Marshal.FreeHGlobal(filterPtr);
                if (titlePtr != IntPtr.Zero) Marshal.FreeHGlobal(titlePtr);
                if (defExtPtr != IntPtr.Zero) Marshal.FreeHGlobal(defExtPtr);
                if (fileBufferPtr != IntPtr.Zero) Marshal.FreeHGlobal(fileBufferPtr);
            }
        }

        private delegate bool DialogInvoker(ref OPENFILENAMEW ofn);

        public static string BuildFilter(params (string Label, string Pattern)[] entries)
        {
            var sb = new StringBuilder();
            foreach (var entry in entries)
            {
                sb.Append(entry.Label);
                sb.Append('\0');
                sb.Append(entry.Pattern);
                sb.Append('\0');
            }

            sb.Append('\0');
            return sb.ToString();
        }
    }
}
