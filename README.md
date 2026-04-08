## Test Scenarios

- Verify main menu loads and displays correctly
- Verify health bar decrements when tank takes damage
- Verify round counter increments on round completion
- Verify winner announcement screen appears on game over
- Verify game restarts correctly from game over screen

## Key Concepts Demonstrated

- **Page Object Model (POM)** applied to game scenes
- **AltTester Driver** as the game equivalent of Selenium WebDriver
- **Game object inspection** equivalent to browser DevTools
- **CI/CD integration** for automated test execution on every commit

## Getting Started

### Prerequisites
- Unity 6 LTS
- Java 11+
- Maven
- AltTester Desktop

### Setup
1. Clone this repository
2. Open the project in Unity Hub
3. Install AltTester SDK into the Unity project
4. Instrument and build the game
5. Run tests via Maven

```bash
mvn test
```

## Presentation

This project was built as part of a QA Automation research presentation exploring game testing automation as an emerging discipline in software quality engineering.