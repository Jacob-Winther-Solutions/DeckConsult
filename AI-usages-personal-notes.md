API usage:
- Sephiroth, Aggro, Aristocrats, Bracket 3:                                                 5,00 -> 4,83 = 0,17 (1st try)
- Sephiroth, Aggro, Aristocrats, Bracket 3:                                                 4,83 -> 4,42 = 0,41 (2nd try)
- Sephiroth, Aggro, Aristocrats, Bracket 3:                                                 4,42 -> 4,20 = 0,22 (3rd try) (After this, classifications of non-plan cards are cached and saved to storage)
- Sephiroth, Aggro, Aristocrats, Bracket 3:                                                 4,20 -> 4,05 = 0,15 (4th try)
- Sephiroth, Aggro, Aristocrats, Bracket 3:                                                 4,05 -> 3,90 = 0,15 (5th try) (custom adjustment of theme)
- Sephiroth, Aggro, Aristocrats, Bracket 3:                                                 3,90 -> 3,68 = 0,22 (6th try) (classification cache reset)
- Sephiroth, Aggro, Aristocrats, Bracket 3:                                                 3,68 -> 3,46 = 0,42 (7th try) (Test new result view)
- Esika, Control, Superfriends, Bracket 4:                                                  3,68 -> 3,22 = 0,46 (8th try) (Added extra tab view)
- Sephiroth, Aggro, Aristocrats, Bracket 3, Budget 15 pr card 300 total:                    3,22 -> 3,05 = 0,17 (9th try) (Test of budget feature)
- Sephiroth, Aggro, Aristocrats, Bracket 3, Budget 10 pr card 300 total:                    3,05 -> 2,92 = 0,13 (10th try) (Re test of budget feature)
- Kozilek, the Great Distortion, Aggro, Big-Mana, Bracket 3, Budget 15 pr card 300 total:   2,92 -> 2,74 = 0,18 (11th try) (Test of Colorless & Wastes support)
- Kozilek, the Great Distortion, Aggro, Big-Mana, Bracket 3, Budget 15 pr card 300 total:   2,74 -> 2,54 = 0,20 (12th try) (Test of Utility Lands include for colorless decks)
- Sephiroth, Aggro, Aristocrats, Bracket 3, Budget 15 pr card 300 total:                    2,54 -> 2,37 = 0,17 (13th try) (Test of Utility Lands include for mono-colored decks)
- Esika, Control, Superfriends, Bracket 3, Budget 15 pr card 300 total:                     2,37 -> 2,17 = 0,20 (14th try) (Test of Utility Lands include for many-colored decks)
- Esika, Control, Superfriends, Bracket 3, Budget 15 pr card 300 total:                     2,17 -> 2,03 = 0,14 (15th try) (Test of EDHREC specific reasoning is removed from spilovers, color fixings and repairs)
- Sephiroth, Aggro, Aristocrats, Bracket 3, Budget 15 pr card 300 total:                    2,03 -> 1,91 = 0,12 (16th try) (Test of export results feature)
- Sephiroth, Aggro, Aristocrats, Bracket 3, Budget 15 pr card 300 total:                    1,91 -> 1,78 = 0,13 (17th try) (Re-test of export results feature)
- Haiku, Sephiroth, Aggro, Aristocrats, Bracket 3, Budget 15 pr card 300 total:             1,78 -> 1,66 = 0,12 (18th try) (BYOK + choose model test)
- Haiku, Sephiroth, Aggro, Aristocrats, No Bracket, Budget Disabled pr card 300 total:      1,66 -> 1,49 = 0,17 (19th try) (No Bracket + No pr card budget test)
- Haiku, Sephiroth, Custom (Aggro, Aristocrats), Budget 15 pr card 300 total:               1,49 -> 1,37 = 0,12 (19th try) (Custom tab test)
- Haiku, Tymna+Kraum, Combo, Pillowfort, No budget:                                         1,37 -> 1,00 = 0,37 (20th try) (Refactoring)
- Haiku, Sephiroth, Aggro, Aristocrats, Budget 15 pr card 300 total:                        1,00 -> 0,88 = 0,12 (21th try) (Fix of refactoring errors)
- Haiku, Sephiroth, Aggro, Aristocrats, Budget 15 pr card 300 total:                        0,88 -> 0,75 = 0,13 (22th try) (LocalStorage test)
- Haiku, Sephiroth, Aggro, Aristocrats, Budget 15 pr card 300 total:                        0,75 -> 0,62 = 0,13 (23th try) (LocalStorage retest)
- Haiku, Sephiroth, Aggro, Aristocrats, Budget 15 pr card 300 total:                        0,62 -> 0,51 = 0,11 (24th try) (Multi LocalStorage test)
- Haiku, Esika, Control, Superfriends, Bracket 3, Budget 15 pr card 300 total:              0,51 -> 0,36 = 0,15 (25th try) (Another Multi LocalStorage test)
- Haiku, Tymna+Kraum, Combo, Pillowfort, Bracket 3, Budget 15 pr card 300 total:            0,36 -> 0,14 = 0,22 (26th try) (Yet Another Multi LocalStorage test)
--------------- Funds refill -> +5 => 5.14 ------------------------
- Kozilek, the Great Distortion, Aggro, Big-Mana, Bracket 3, Budget 15 pr card 300 total:   5,14 -> 5,02 = 0,12 (27th try) (Final Multi LocalStorage test)

Bad classifications:
Sephiroth (third try):
- "Takenuma, Abondonded Mire" is not card advantage, you discard the land and gets one creature or planeswalker back, so you net zero cards. It is indeed recursion.

UI:
- Coverages are shown as decimals, which can be confusing.


Notes:
- Check if it is possible to see how far each step has gotten when running the builder/LLM.
- Consider adding a "must-include" feature to the builder, where the user can register cards to the 99 that must be in the deck. The LLM can suggest to cut them, but they must be classified, evaluated and included.