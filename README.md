# Day & Night Simulation 3D

**Autor**: Patryk Hołubowicz
**Technologie**: C#, OpenTK, OpenGL, GLSL

## 🎯 Opis projektu

Interaktywna symulacja 3D przedstawiająca realistyczny cykl dnia i nocy w trójwymiarowym środowisku.

### ✨ Główne funkcjonalności

- **Cykl dnia i nocy** - dynamiczna zmiana czasu z płynnymi przejściami
- **Realistyczne oświetlenie** - model Phonga z różnymi źródłami światła (słońce/księżyc)
- **Interaktywna kamera** - pełne sterowanie z ograniczeniami terenu
- **Dynamiczna atmosfera** - chmury, gwiazdy, słońce i księżyc
- **Różnorodne obiekty** - drzewa, domki, kamienie, teren
- **Efekty wizualne** - blending, przezroczystość, animacje

## 🚀 Instrukcja uruchomienia
TBA

### Sterowanie

- **W/S/A/D** - poruszanie kamerą (przód/tył/lewo/prawo)
- **Q/E** - wznoszenie/opadanie kamery
- **Strzałki** - obrót kamery (góra/dół/lewo/prawo)
- **P** - przyspieszanie czasu do przodu
- **O** - cofanie czasu
- **ESC** - wyjście z programu

### Informacje wyświetlane w konsoli
Program wyświetla w konsoli:
- Aktualny czas symulacji
- Pozycję kamery
- Liczbę gwiazd i chmur
- Granice ruchu kamery

## 📚 Lista użytych bibliotek i assetów

### Biblioteki programistyczne
- **OpenTK 4.7.7** - binding OpenGL dla platformy .NET
- **OpenGL 3.3** - API grafiki 3D
- **GLSL** - język shaderów OpenGL
- **.NET Framework 4.7.2** - platforma wykonawcza
- **System.Drawing** - obsługa tekstur bitmapowych

### Asset-y graficzne
- **Tekstura trawy** - `Assets/grass.jpg`
