using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace ScreenProtector.Tests
{
    public class SettingsManagerTests
    {
        [Fact]
        public void SettingsManager_LoadSave_PreservesSettings()
        {
            // Arrange
            SettingsManager.Load();
            var originalEffect = SettingsManager.Current.EffectType;
            SettingsManager.Current.EffectType = originalEffect == 1 ? 2 : 1;

            // Act
            SettingsManager.Save();
            var savedEffect = SettingsManager.Current.EffectType;
            SettingsManager.Current.EffectType = originalEffect; // Reset state for a moment
            SettingsManager.Load();

            // Assert
            Assert.Equal(savedEffect, SettingsManager.Current.EffectType);

            // Cleanup
            SettingsManager.Current.EffectType = originalEffect;
            SettingsManager.Save();
        }
    }
}
