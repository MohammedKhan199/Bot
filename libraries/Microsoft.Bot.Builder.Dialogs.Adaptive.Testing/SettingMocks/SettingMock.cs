// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Bot.Builder.Adapters;
using Microsoft.Bot.Builder.Dialogs.Debugging;

namespace Microsoft.Bot.Builder.Dialogs.Adaptive.Testing.SettingMocks
{
    /// <summary>
    /// Abstract class for mocking settings.
    /// </summary>
    public abstract class SettingMock
    {
        /// <summary>
        /// Registers the source path.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <param name="line">The caller's line.</param>
        protected void RegisterSourcePath(string path, int line)
        {
            if (!string.IsNullOrEmpty(path))
            {
                DebugSupport.SourceMap.Add(this, new SourceRange() { Path = path, StartPoint = new SourcePoint() { LineIndex = line, CharIndex = 0 }, EndPoint = new SourcePoint() { LineIndex = line + 1, CharIndex = 0 }, });
            }
        }
    }
}
