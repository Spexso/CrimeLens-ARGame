# 🔍 CrimeLens AR — Murder Mystery Detective Game

> An augmented reality mobile game that transforms any physical space into an interactive crime scene using computer vision, AI-generated narratives, and augmented reality.

---

## Overview

CrimeLens AR combines three technologies to deliver a unique gameplay experience:

- **Computer Vision (YOLOv8)** — Detects real-world objects in the player's environment
- **Artificial Intelligence (Google Gemini 2.0 Flash)** — Generates unique murder mystery scenarios from detected objects
- **Augmented Reality (AR Foundation)** — Overlays virtual crime scene props and markers onto the real world

Every room becomes a unique, playable crime scene.

---

## Game Flow

```
Login → Setup Crime Scene → Scan Room → Mystery Generation → Investigate → Solve → Results
```

1. **Login** — Enter a username (no password required)
2. **Setup Crime Scene** — AR chalk outlines and barriers are placed in your room
3. **Scan Room** — Point camera at objects; YOLO detects potential murder weapons
4. **Mystery Generation** — Gemini AI creates a murder story using your detected objects
5. **Investigation** — Explore the AR crime scene and examine clues
6. **Solution** — Tap the correct murder weapon marker to solve the case
7. **Results** — Win/lose screen with stats saved to the leaderboard

---

## Technology Stack

| Category | Technology |
|---|---|
| Engine | Unity 6 |
| AR Framework | AR Foundation + ARCore (Android) / ARKit (iOS) |
| Object Detection | YOLOv8n — custom-trained, runs fully on-device |
| ML Inference | Unity Inference Engine |
| AI Narrative | Google Gemini 2.0 Flash (via Firebase Cloud Functions) |
| Backend | Firebase — Auth, Realtime Database, Cloud Functions |
| Training Platform | Roboflow |

---

## Architecture

The project is organized into distinct layers, each with a clear responsibility:

- **Presentation** — Screens and UI managers for each game phase
- **Game Logic** — Central state machine (`GameManager`) coordinating all subsystems
- **AR & ML** — Camera capture, YOLO inference, and AR marker placement
- **Network** — Secure Gemini API proxy via Firebase Cloud Functions
- **Data** — Firebase anonymous auth linked to usernames, Realtime Database for stats

---

## Object Detection

The custom YOLOv8n model detects the following murder mystery objects:

```
key  |  knife  |  scissors  |  screwdriver  |  frying pan  |  chair
```

The model runs entirely on-device with no internet required for detection. A single frame is captured from the AR camera, preprocessed, run through inference, and the best detection is used to place an AR marker in the scene.

> **Key learning:** Matching the preprocessing pipeline (letterboxing, padding color, normalization) exactly to the Roboflow training configuration was critical — this single change improved detection confidence from ~4% to ~79%.

---

## Setup

### Requirements

- Unity 6 with Android Build Support
- Android 7.0+ device with ARCore, or iPhone 6S+ with ARKit
- Firebase project (free Spark plan, Blaze required for Cloud Functions)
- Google Gemini API key

### Steps

1. Clone the repository and open in Unity 6
2. Install required packages via Package Manager: `AR Foundation`, `ARCore/ARKit XR Plugin`, `Newtonsoft.Json`
3. Enable ARCore (Android) and/or ARKit (iOS) in **Project Settings → XR Plugin Management**
4. Set up a Firebase project, enable Anonymous Auth and Realtime Database, and place `google-services.json` in `Assets/`
5. Deploy the `generateMystery` Cloud Function as a secure Gemini API proxy
6. Assign the custom `best.onnx` YOLO model and `YOLOClassNames` ScriptableObject in the scene
7. Build and deploy to device

---

## Firebase Database Structure

```
users/
  <user_id>/
    username, total_mysteries, total_solved, fastest_time, current_streak

usernames/
  <username_lowercase>: <user_id>

mysteries/
  <mystery_id>/
    user_id, is_solved, completion_time, timestamp
```

---

## Leaderboard

Top 10 players ranked by most mysteries solved, then by current streak, then by fastest completion time.

---

## Target Platforms

- **Primary:** Android 7.0+ (ARCore)
- **Secondary:** iOS 11.0+ / iPhone 6S+ (ARKit)

---

*Developed as a graduation project integrating mobile AR, on-device machine learning, and generative AI.*
