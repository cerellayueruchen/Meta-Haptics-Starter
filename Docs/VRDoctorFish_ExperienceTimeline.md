# VR Doctor Fish: Full Experience Timeline

Total timed experience: **2:01**

## Physical setup (before 0:00)

The user sits down, removes shoes, places both feet in the real bucket of water and puts on the Quest 3 headset. The timed sequence begins when **Start Experience** is activated.

## Timeline

| Timestamp | Scene | Visual, audio and haptic action |
| --- | --- | --- |
| 0:00–0:12 | Scene 1: Welcome / Entering the Water | Clear water flows around the virtual feet. A water-entry sound plays at 0:00. Background music fades in from 0:00–0:02. A gentle vibration pattern moves along both legs, introducing the water sensation. |
| 0:12–0:57 | Scene 2: Small Fish | Small fish enter and swim towards the feet. They nibble at random locations, producing short ticklish vibrations and nibbling sounds. Individual nibble timestamps are procedural, not fixed. |
| 0:57–1:19 | Scene 3: Big Fish | Small fish withdraw. Lighting darkens and the water becomes rougher. A large fish enters, circles the feet, bites each foot, then retreats. |
| 1:19–1:41 | Scene 4: Jellyfish | The environment shifts to purple and blue lighting. Jellyfish drift around the legs. Contact with either foot triggers a sharp electrical sound and a short high-frequency sting vibration. Exact sting timestamps depend on procedural contact. Each jellyfish has a five-second sting cooldown. |
| 1:41–2:01 | Scene 5: Calm / Recovery | Small fish, the big fish and jellyfish return to gentle swimming. They no longer nibble, bite or sting. Water movement settles and the warmer lighting returns. Vibrations fade away. |
| 1:51–1:59 | Music fade-out | The background music begins fading halfway through the Calm scene and fades over eight seconds. |
| 2:01 | Session Complete | The timed sequence ends. Creatures continue swimming gently until the experience is restarted or the headset is removed. |

## Scene 3 detail: Big Fish choreography

| Timestamp | Beat | Action |
| --- | --- | --- |
| 0:57–1:04 | Big fish approach | The fish completes one slow circle around the feet. |
| Approx. 1:05.7 | First bite | The fish lines up for 1.2 seconds, lunges for 0.45 seconds, then triggers the first bite sound and strong vibration. |
| Approx. 1:06–1:07.2 | First bite hold and release | The fish shakes for 0.7 seconds, then backs away for 0.8 seconds. |
| Approx. 1:10.4 | Second bite | After a 1.6-second pause, the fish attacks the opposite foot. The second bite sound and vibration trigger on contact. |
| Approx. 1:10–1:12 | Second bite hold and release | The fish shakes, releases the foot and swims away. Remaining time allows the retreat animation to finish. |

The bite timestamps are approximate because animation timing advances frame by frame. The bite choreography specifies a seven-second circle, 1.2-second alignment, 0.45-second lunge, 0.7-second shake and 0.8-second release.

## Implementation notes

- The fixed stage durations are 12, 45, 22, 22 and 20 seconds.
- Each stage's lighting, fog and water appearance blends over 2.5 seconds, rather than changing instantly.
- The stage order and Calm-stage music fade are controlled directly by the experience state manager (`Assets/Scripts/DoctorFish/ExperienceStateManager.cs`).
- Nibble and sting events are procedural within their stages, not fixed timestamps.
