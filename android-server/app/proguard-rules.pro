# Reglas Proguard básicas para TowerShare
-keepattributes *Annotation*
-keepclassmembers class * {
    native <methods>;
}
