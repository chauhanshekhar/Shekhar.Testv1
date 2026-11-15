# PUT live-condition variants (2026-09-30..2026-10-28, 19 days, 15-min window)

Simulated only (snapshots + outcomes; no live signals or trades). Live rule, every on/off combination of its conditions, then one condition changed at a time (plus RSI x PCR joint grid).

| Cell | train_N | train_Net | train_PF | val_N | val_Net | val_PF | hold_N | hold_Days | hold_Net | hold_PF | all_N | all_Days | all_TgtPct | all_Net | all_PF | all_Exp | all_MaxDD | BeatsLive_all3 | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| LIVE (current settings) | 465 | -34940 | 0.58 | 161 | -16980 | 0.5 | 102 | 3 | -11770 | 0.48 | 728 | 18 | 52.9 | -63690 | 0.54 | -87.5 | 64090 | False | False |
| All live conditions off (trend rows only) | 1565 | -122030 | 0.56 | 940 | -86220 | 0.52 | 770 | 4 | -79570 | 0.49 | 3275 | 19 | 51.9 | -287820 | 0.53 | -87.9 | 289220 | False | False |

## Interchange (live conditions on/off)

| Cell | train_N | train_Net | train_PF | val_N | val_Net | val_PF | hold_N | hold_Days | hold_Net | hold_PF | all_N | all_Days | all_TgtPct | all_Net | all_PF | all_Exp | all_MaxDD | BeatsLive_all3 | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| OFF: SENSEX-PCRmin | ON: LTP-EMA20, EMAgap, RSI, GapUpRSI, PCR, ATR | 466 | -34820 | 0.58 | 161 | -16980 | 0.5 | 102 | 3 | -11770 | 0.48 | 729 | 18 | 53.0 | -63570 | 0.54 | -87.2 | 63970 | False | False |
| OFF: SENSEX-PCRmin, ATR | ON: LTP-EMA20, EMAgap, RSI, GapUpRSI, PCR | 487 | -35780 | 0.58 | 171 | -18020 | 0.5 | 104 | 3 | -11170 | 0.5 | 762 | 18 | 53.3 | -64970 | 0.55 | -85.3 | 65370 | False | False |
| OFF: ATR | ON: LTP-EMA20, EMAgap, RSI, GapUpRSI, PCR, SENSEX-PCRmin | 486 | -35900 | 0.58 | 171 | -18020 | 0.5 | 104 | 3 | -11170 | 0.5 | 761 | 18 | 53.2 | -65090 | 0.55 | -85.5 | 65490 | False | False |
| OFF: GapUpRSI, SENSEX-PCRmin | ON: LTP-EMA20, EMAgap, RSI, PCR, ATR | 419 | -37040 | 0.52 | 191 | -18480 | 0.51 | 107 | 3 | -11010 | 0.52 | 717 | 16 | 51.7 | -66530 | 0.52 | -92.8 | 66430 | False | False |
| OFF: GapUpRSI | ON: LTP-EMA20, EMAgap, RSI, PCR, SENSEX-PCRmin, ATR | 418 | -37160 | 0.52 | 191 | -18480 | 0.51 | 107 | 3 | -11010 | 0.52 | 716 | 16 | 51.6 | -66650 | 0.52 | -93.1 | 66550 | False | False |
| OFF: GapUpRSI, SENSEX-PCRmin, ATR | ON: LTP-EMA20, EMAgap, RSI, PCR | 444 | -37520 | 0.53 | 194 | -19560 | 0.49 | 109 | 3 | -10410 | 0.55 | 747 | 16 | 52.1 | -67490 | 0.52 | -90.3 | 67390 | False | False |
| OFF: GapUpRSI, ATR | ON: LTP-EMA20, EMAgap, RSI, PCR, SENSEX-PCRmin | 443 | -37640 | 0.53 | 194 | -19560 | 0.49 | 109 | 3 | -10410 | 0.55 | 746 | 16 | 52.0 | -67610 | 0.52 | -90.6 | 67510 | False | False |
| OFF: PCR, SENSEX-PCRmin | ON: LTP-EMA20, EMAgap, RSI, GapUpRSI, ATR | 479 | -36060 | 0.57 | 183 | -18370 | 0.51 | 162 | 3 | -13590 | 0.58 | 824 | 18 | 53.8 | -68020 | 0.56 | -82.5 | 68420 | False | False |
| OFF: PCR | ON: LTP-EMA20, EMAgap, RSI, GapUpRSI, SENSEX-PCRmin, ATR | 478 | -36180 | 0.57 | 183 | -18370 | 0.51 | 162 | 3 | -13590 | 0.58 | 823 | 18 | 53.7 | -68140 | 0.56 | -82.8 | 68540 | False | False |
| OFF: PCR, SENSEX-PCRmin, ATR | ON: LTP-EMA20, EMAgap, RSI, GapUpRSI | 506 | -36700 | 0.58 | 195 | -19970 | 0.5 | 169 | 4 | -12590 | 0.61 | 870 | 19 | 54.2 | -69260 | 0.57 | -79.6 | 69660 | False | False |
| OFF: PCR, ATR | ON: LTP-EMA20, EMAgap, RSI, GapUpRSI, SENSEX-PCRmin | 505 | -36820 | 0.58 | 195 | -19970 | 0.5 | 169 | 4 | -12590 | 0.61 | 869 | 19 | 54.1 | -69380 | 0.57 | -79.8 | 69780 | False | False |
| OFF: EMAgap, GapUpRSI, SENSEX-PCRmin | ON: LTP-EMA20, RSI, PCR, ATR | 435 | -37570 | 0.53 | 196 | -19980 | 0.49 | 108 | 3 | -12060 | 0.49 | 739 | 16 | 51.8 | -69610 | 0.52 | -94.2 | 70310 | False | False |
| OFF: EMAgap, GapUpRSI | ON: LTP-EMA20, RSI, PCR, SENSEX-PCRmin, ATR | 434 | -37690 | 0.53 | 196 | -19980 | 0.49 | 108 | 3 | -12060 | 0.49 | 738 | 16 | 51.7 | -69730 | 0.51 | -94.5 | 70430 | False | False |
| OFF: EMAgap, SENSEX-PCRmin | ON: LTP-EMA20, RSI, GapUpRSI, PCR, ATR | 539 | -39250 | 0.59 | 189 | -19660 | 0.51 | 106 | 3 | -11340 | 0.5 | 834 | 18 | 53.3 | -70250 | 0.56 | -84.2 | 70850 | False | False |
| OFF: EMAgap | ON: LTP-EMA20, RSI, GapUpRSI, PCR, SENSEX-PCRmin, ATR | 538 | -39370 | 0.59 | 189 | -19660 | 0.51 | 106 | 3 | -11340 | 0.5 | 833 | 18 | 53.2 | -70370 | 0.56 | -84.5 | 70970 | False | False |
| OFF: EMAgap, GapUpRSI, SENSEX-PCRmin, ATR | ON: LTP-EMA20, RSI, PCR | 460 | -38050 | 0.54 | 199 | -21060 | 0.48 | 110 | 3 | -11460 | 0.51 | 769 | 16 | 52.2 | -70570 | 0.52 | -91.8 | 71270 | False | False |
| OFF: EMAgap, GapUpRSI, ATR | ON: LTP-EMA20, RSI, PCR, SENSEX-PCRmin | 459 | -38170 | 0.54 | 199 | -21060 | 0.48 | 110 | 3 | -11460 | 0.51 | 768 | 16 | 52.1 | -70690 | 0.52 | -92.0 | 71390 | False | False |
| OFF: LTP-EMA20, GapUpRSI | ON: EMAgap, RSI, PCR, SENSEX-PCRmin, ATR | 495 | -36980 | 0.56 | 240 | -17570 | 0.59 | 134 | 3 | -16280 | 0.46 | 869 | 16 | 53.2 | -70830 | 0.55 | -81.5 | 70430 | False | False |
| OFF: LTP-EMA20, GapUpRSI, SENSEX-PCRmin | ON: EMAgap, RSI, PCR, ATR | 497 | -37140 | 0.56 | 240 | -17570 | 0.59 | 137 | 3 | -17120 | 0.45 | 874 | 16 | 53.0 | -71830 | 0.55 | -82.2 | 71430 | False | False |
| OFF: LTP-EMA20 | ON: EMAgap, RSI, GapUpRSI, PCR, SENSEX-PCRmin, ATR | 560 | -37070 | 0.61 | 206 | -18000 | 0.55 | 130 | 3 | -17000 | 0.43 | 896 | 18 | 53.9 | -72070 | 0.56 | -80.4 | 72870 | False | False |
| OFF: LTP-EMA20, GapUpRSI, ATR | ON: EMAgap, RSI, PCR, SENSEX-PCRmin | 522 | -38160 | 0.57 | 243 | -18250 | 0.58 | 136 | 3 | -15680 | 0.48 | 901 | 16 | 53.6 | -72090 | 0.56 | -80.0 | 71690 | False | False |
| OFF: EMAgap, ATR | ON: LTP-EMA20, RSI, GapUpRSI, PCR, SENSEX-PCRmin | 562 | -41580 | 0.58 | 202 | -20630 | 0.51 | 108 | 3 | -10740 | 0.53 | 872 | 18 | 53.3 | -72950 | 0.55 | -83.7 | 73550 | False | False |
| OFF: LTP-EMA20, SENSEX-PCRmin | ON: EMAgap, RSI, GapUpRSI, PCR, ATR | 562 | -37230 | 0.61 | 206 | -18000 | 0.55 | 133 | 3 | -17840 | 0.41 | 901 | 18 | 53.7 | -73070 | 0.56 | -81.1 | 73870 | False | False |
| OFF: LTP-EMA20, GapUpRSI, SENSEX-PCRmin, ATR | ON: EMAgap, RSI, PCR | 524 | -38320 | 0.57 | 243 | -18250 | 0.58 | 139 | 3 | -16520 | 0.47 | 906 | 16 | 53.4 | -73090 | 0.55 | -80.7 | 72690 | False | False |
| OFF: EMAgap, SENSEX-PCRmin, ATR | ON: LTP-EMA20, RSI, GapUpRSI, PCR | 563 | -41460 | 0.58 | 205 | -21070 | 0.5 | 108 | 3 | -10740 | 0.53 | 876 | 18 | 53.3 | -73270 | 0.55 | -83.6 | 73870 | False | False |
| OFF: LTP-EMA20, ATR | ON: EMAgap, RSI, GapUpRSI, PCR, SENSEX-PCRmin | 583 | -38330 | 0.6 | 217 | -18920 | 0.54 | 132 | 3 | -16400 | 0.45 | 932 | 18 | 54.2 | -73650 | 0.56 | -79.0 | 74450 | False | False |
| OFF: EMAgap, PCR, SENSEX-PCRmin | ON: LTP-EMA20, RSI, GapUpRSI, ATR | 554 | -40290 | 0.59 | 211 | -21050 | 0.52 | 169 | 3 | -12650 | 0.61 | 934 | 18 | 54.0 | -73990 | 0.58 | -79.2 | 74590 | False | False |
| OFF: EMAgap, PCR | ON: LTP-EMA20, RSI, GapUpRSI, SENSEX-PCRmin, ATR | 553 | -40410 | 0.59 | 211 | -21050 | 0.52 | 169 | 3 | -12650 | 0.61 | 933 | 18 | 54.0 | -74110 | 0.57 | -79.4 | 74710 | False | False |
| OFF: LTP-EMA20, EMAgap, GapUpRSI | ON: RSI, PCR, SENSEX-PCRmin, ATR | 514 | -36350 | 0.59 | 253 | -20520 | 0.58 | 135 | 3 | -17330 | 0.43 | 902 | 16 | 53.3 | -74200 | 0.56 | -82.3 | 74600 | False | False |
| OFF: LTP-EMA20, SENSEX-PCRmin, ATR | ON: EMAgap, RSI, GapUpRSI, PCR | 585 | -38490 | 0.6 | 219 | -19080 | 0.54 | 135 | 3 | -17240 | 0.43 | 939 | 18 | 54.0 | -74810 | 0.56 | -79.7 | 75610 | False | False |
| OFF: LTP-EMA20, EMAgap, GapUpRSI, SENSEX-PCRmin | ON: RSI, PCR, ATR | 516 | -36510 | 0.59 | 253 | -20520 | 0.58 | 138 | 3 | -18170 | 0.42 | 907 | 16 | 53.1 | -75200 | 0.55 | -82.9 | 75600 | False | False |
| OFF: LTP-EMA20, PCR | ON: EMAgap, RSI, GapUpRSI, SENSEX-PCRmin, ATR | 574 | -38110 | 0.6 | 231 | -19430 | 0.55 | 202 | 3 | -18080 | 0.56 | 1007 | 18 | 54.8 | -75620 | 0.58 | -75.1 | 76420 | False | False |
| OFF: LTP-EMA20, EMAgap, GapUpRSI, ATR | ON: RSI, PCR, SENSEX-PCRmin | 542 | -37930 | 0.59 | 256 | -21200 | 0.57 | 137 | 3 | -16730 | 0.45 | 935 | 16 | 53.7 | -75860 | 0.56 | -81.1 | 76260 | False | False |
| OFF: LTP-EMA20, PCR, SENSEX-PCRmin | ON: EMAgap, RSI, GapUpRSI, ATR | 576 | -38270 | 0.6 | 231 | -19430 | 0.55 | 205 | 3 | -18920 | 0.55 | 1012 | 18 | 54.6 | -76620 | 0.58 | -75.7 | 77420 | False | False |
| OFF: LTP-EMA20, EMAgap, GapUpRSI, SENSEX-PCRmin, ATR | ON: RSI, PCR | 544 | -38090 | 0.58 | 256 | -21200 | 0.57 | 140 | 3 | -17570 | 0.44 | 940 | 16 | 53.5 | -76860 | 0.55 | -81.8 | 77260 | False | False |
| OFF: LTP-EMA20, PCR, ATR | ON: EMAgap, RSI, GapUpRSI, SENSEX-PCRmin | 603 | -39050 | 0.61 | 244 | -20910 | 0.54 | 209 | 4 | -17080 | 0.59 | 1056 | 19 | 55.2 | -77040 | 0.59 | -73.0 | 77840 | False | False |
| OFF: GapUpRSI, PCR, SENSEX-PCRmin | ON: LTP-EMA20, EMAgap, RSI, ATR | 458 | -41040 | 0.51 | 221 | -20510 | 0.51 | 208 | 3 | -15870 | 0.58 | 887 | 16 | 52.6 | -77420 | 0.53 | -87.3 | 77320 | False | False |
| OFF: EMAgap, PCR, ATR | ON: LTP-EMA20, RSI, GapUpRSI, SENSEX-PCRmin | 588 | -43300 | 0.58 | 226 | -22580 | 0.51 | 176 | 4 | -11650 | 0.65 | 990 | 19 | 54.1 | -77530 | 0.57 | -78.3 | 78130 | False | False |
| OFF: GapUpRSI, PCR | ON: LTP-EMA20, EMAgap, RSI, SENSEX-PCRmin, ATR | 457 | -41160 | 0.51 | 221 | -20510 | 0.51 | 208 | 3 | -15870 | 0.58 | 886 | 16 | 52.5 | -77540 | 0.53 | -87.5 | 77440 | False | False |
| OFF: EMAgap, PCR, SENSEX-PCRmin, ATR | ON: LTP-EMA20, RSI, GapUpRSI | 589 | -43180 | 0.58 | 229 | -23020 | 0.5 | 176 | 4 | -11650 | 0.65 | 994 | 19 | 54.1 | -77850 | 0.57 | -78.3 | 78450 | False | False |
| OFF: LTP-EMA20, PCR, SENSEX-PCRmin, ATR | ON: EMAgap, RSI, GapUpRSI | 605 | -39210 | 0.61 | 246 | -21070 | 0.54 | 212 | 4 | -17920 | 0.57 | 1063 | 19 | 55.1 | -78200 | 0.58 | -73.6 | 79000 | False | False |
| OFF: GapUpRSI, PCR, SENSEX-PCRmin, ATR | ON: LTP-EMA20, EMAgap, RSI | 488 | -41320 | 0.53 | 226 | -22150 | 0.49 | 215 | 4 | -14870 | 0.61 | 929 | 17 | 53.1 | -78340 | 0.54 | -84.3 | 78240 | False | False |
| OFF: GapUpRSI, PCR, ATR | ON: LTP-EMA20, EMAgap, RSI, SENSEX-PCRmin | 487 | -41440 | 0.53 | 226 | -22150 | 0.49 | 215 | 4 | -14870 | 0.61 | 928 | 17 | 53.0 | -78460 | 0.54 | -84.5 | 78360 | False | False |
| OFF: EMAgap, GapUpRSI, PCR, SENSEX-PCRmin | ON: LTP-EMA20, RSI, ATR | 475 | -41450 | 0.53 | 226 | -22010 | 0.5 | 213 | 3 | -15930 | 0.59 | 914 | 16 | 52.9 | -79390 | 0.54 | -86.9 | 80090 | False | False |
| OFF: EMAgap, GapUpRSI, PCR | ON: LTP-EMA20, RSI, SENSEX-PCRmin, ATR | 474 | -41570 | 0.53 | 226 | -22010 | 0.5 | 213 | 3 | -15930 | 0.59 | 913 | 16 | 52.8 | -79510 | 0.53 | -87.1 | 80210 | False | False |
| OFF: EMAgap, GapUpRSI, PCR, SENSEX-PCRmin, ATR | ON: LTP-EMA20, RSI | 505 | -41730 | 0.54 | 231 | -23650 | 0.48 | 220 | 4 | -14930 | 0.62 | 956 | 17 | 53.3 | -80310 | 0.54 | -84.0 | 81010 | False | False |
| OFF: EMAgap, GapUpRSI, PCR, ATR | ON: LTP-EMA20, RSI, SENSEX-PCRmin | 504 | -41850 | 0.54 | 231 | -23650 | 0.48 | 220 | 4 | -14930 | 0.62 | 955 | 17 | 53.3 | -80430 | 0.54 | -84.2 | 81130 | False | False |
| OFF: LTP-EMA20, EMAgap | ON: RSI, GapUpRSI, PCR, SENSEX-PCRmin, ATR | 657 | -43340 | 0.61 | 259 | -20760 | 0.58 | 135 | 3 | -16450 | 0.45 | 1051 | 18 | 54.3 | -80550 | 0.58 | -76.6 | 81550 | False | False |
| OFF: LTP-EMA20, GapUpRSI, PCR | ON: EMAgap, RSI, SENSEX-PCRmin, ATR | 537 | -41580 | 0.55 | 273 | -19640 | 0.59 | 248 | 3 | -20280 | 0.56 | 1058 | 16 | 54.0 | -81500 | 0.57 | -77.0 | 81100 | False | False |
| OFF: LTP-EMA20, EMAgap, SENSEX-PCRmin | ON: RSI, GapUpRSI, PCR, ATR | 659 | -43500 | 0.61 | 259 | -20760 | 0.58 | 138 | 3 | -17290 | 0.44 | 1056 | 18 | 54.2 | -81550 | 0.58 | -77.2 | 82550 | False | False |
| OFF: LTP-EMA20, GapUpRSI, PCR, SENSEX-PCRmin | ON: EMAgap, RSI, ATR | 539 | -41740 | 0.55 | 273 | -19640 | 0.59 | 251 | 3 | -21120 | 0.55 | 1063 | 16 | 53.8 | -82500 | 0.56 | -77.6 | 82100 | False | False |
| OFF: LTP-EMA20, GapUpRSI, PCR, ATR | ON: EMAgap, RSI, SENSEX-PCRmin | 569 | -42560 | 0.56 | 278 | -20880 | 0.58 | 255 | 4 | -19280 | 0.59 | 1102 | 17 | 54.4 | -82720 | 0.57 | -75.1 | 82320 | False | False |
| OFF: LTP-EMA20, EMAgap, PCR | ON: RSI, GapUpRSI, SENSEX-PCRmin, ATR | 675 | -44520 | 0.61 | 284 | -22190 | 0.58 | 210 | 3 | -16370 | 0.6 | 1169 | 18 | 55.2 | -83080 | 0.6 | -71.1 | 84080 | False | False |
| OFF: LTP-EMA20, EMAgap, GapUpRSI, PCR | ON: RSI, SENSEX-PCRmin, ATR | 557 | -40830 | 0.57 | 286 | -22590 | 0.58 | 253 | 3 | -19690 | 0.58 | 1096 | 16 | 54.3 | -83110 | 0.58 | -75.8 | 83510 | False | False |
| OFF: LTP-EMA20, GapUpRSI, PCR, SENSEX-PCRmin, ATR | ON: EMAgap, RSI | 571 | -42720 | 0.56 | 278 | -20880 | 0.58 | 258 | 4 | -20120 | 0.58 | 1107 | 17 | 54.3 | -83720 | 0.57 | -75.6 | 83320 | False | False |
| OFF: LTP-EMA20, EMAgap, PCR, SENSEX-PCRmin | ON: RSI, GapUpRSI, ATR | 677 | -44680 | 0.61 | 284 | -22190 | 0.58 | 213 | 3 | -17210 | 0.59 | 1174 | 18 | 55.0 | -84080 | 0.6 | -71.6 | 85080 | False | False |
| OFF: LTP-EMA20, EMAgap, GapUpRSI, PCR, SENSEX-PCRmin | ON: RSI, ATR | 559 | -40990 | 0.57 | 286 | -22590 | 0.58 | 256 | 3 | -20530 | 0.57 | 1101 | 16 | 54.1 | -84110 | 0.57 | -76.4 | 84510 | False | False |
| OFF: LTP-EMA20, EMAgap, ATR | ON: RSI, GapUpRSI, PCR, SENSEX-PCRmin | 687 | -46690 | 0.6 | 273 | -21610 | 0.58 | 137 | 3 | -15850 | 0.47 | 1097 | 18 | 54.4 | -84150 | 0.57 | -76.7 | 84750 | False | False |
| OFF: LTP-EMA20, EMAgap, GapUpRSI, PCR, ATR | ON: RSI, SENSEX-PCRmin | 590 | -42210 | 0.58 | 291 | -23830 | 0.57 | 260 | 4 | -18690 | 0.61 | 1141 | 17 | 54.7 | -84730 | 0.58 | -74.3 | 85130 | False | False |
| OFF: LTP-EMA20, EMAgap, GapUpRSI, PCR, SENSEX-PCRmin, ATR | ON: RSI | 592 | -42370 | 0.58 | 291 | -23830 | 0.57 | 263 | 4 | -19530 | 0.6 | 1146 | 17 | 54.5 | -85730 | 0.58 | -74.8 | 86130 | False | False |
| OFF: LTP-EMA20, EMAgap, SENSEX-PCRmin, ATR | ON: RSI, GapUpRSI, PCR | 689 | -46850 | 0.6 | 278 | -22210 | 0.57 | 140 | 3 | -16690 | 0.46 | 1107 | 18 | 54.2 | -85750 | 0.57 | -77.5 | 86350 | False | False |
| OFF: LTP-EMA20, EMAgap, PCR, ATR | ON: RSI, GapUpRSI, SENSEX-PCRmin | 715 | -48380 | 0.6 | 300 | -23600 | 0.58 | 217 | 4 | -15370 | 0.63 | 1232 | 19 | 55.2 | -87350 | 0.6 | -70.9 | 87950 | False | False |
| OFF: LTP-EMA20, EMAgap, PCR, SENSEX-PCRmin, ATR | ON: RSI, GapUpRSI | 717 | -48540 | 0.6 | 305 | -24200 | 0.57 | 220 | 4 | -16210 | 0.62 | 1242 | 19 | 55.0 | -88950 | 0.6 | -71.6 | 89550 | False | False |
| OFF: RSI | ON: LTP-EMA20, EMAgap, GapUpRSI, PCR, SENSEX-PCRmin, ATR | 724 | -60010 | 0.55 | 333 | -39710 | 0.44 | 234 | 4 | -31970 | 0.4 | 1291 | 19 | 50.1 | -131690 | 0.49 | -102.0 | 131890 | False | False |
| OFF: RSI, SENSEX-PCRmin | ON: LTP-EMA20, EMAgap, GapUpRSI, PCR, ATR | 726 | -60170 | 0.55 | 335 | -40270 | 0.43 | 234 | 4 | -31970 | 0.4 | 1295 | 19 | 50.0 | -132410 | 0.49 | -102.2 | 132610 | False | False |
| OFF: RSI, ATR | ON: LTP-EMA20, EMAgap, GapUpRSI, PCR, SENSEX-PCRmin | 767 | -63670 | 0.55 | 352 | -41610 | 0.44 | 250 | 4 | -32870 | 0.41 | 1369 | 19 | 50.4 | -138150 | 0.49 | -100.9 | 138350 | False | False |
| OFF: RSI, SENSEX-PCRmin, ATR | ON: LTP-EMA20, EMAgap, GapUpRSI, PCR | 769 | -63830 | 0.55 | 354 | -42170 | 0.43 | 250 | 4 | -32870 | 0.41 | 1373 | 19 | 50.3 | -138870 | 0.49 | -101.1 | 139070 | False | False |
| OFF: RSI, PCR | ON: LTP-EMA20, EMAgap, GapUpRSI, SENSEX-PCRmin, ATR | 743 | -61090 | 0.55 | 382 | -40780 | 0.46 | 386 | 4 | -44030 | 0.46 | 1511 | 19 | 50.8 | -145900 | 0.5 | -96.6 | 146100 | False | False |
| OFF: RSI, PCR, SENSEX-PCRmin | ON: LTP-EMA20, EMAgap, GapUpRSI, ATR | 745 | -61250 | 0.55 | 384 | -41340 | 0.46 | 386 | 4 | -44030 | 0.46 | 1515 | 19 | 50.7 | -146620 | 0.5 | -96.8 | 146820 | False | False |
| OFF: RSI, GapUpRSI | ON: LTP-EMA20, EMAgap, PCR, SENSEX-PCRmin, ATR | 853 | -71480 | 0.54 | 451 | -44640 | 0.49 | 243 | 4 | -31090 | 0.43 | 1547 | 19 | 51.0 | -147210 | 0.51 | -95.2 | 147810 | False | False |
| OFF: RSI, GapUpRSI, SENSEX-PCRmin | ON: LTP-EMA20, EMAgap, PCR, ATR | 855 | -71640 | 0.54 | 453 | -45200 | 0.49 | 243 | 4 | -31090 | 0.43 | 1551 | 19 | 50.9 | -147930 | 0.51 | -95.4 | 148530 | False | False |
| OFF: RSI, PCR, ATR | ON: LTP-EMA20, EMAgap, GapUpRSI, SENSEX-PCRmin | 799 | -63990 | 0.55 | 403 | -43240 | 0.46 | 409 | 4 | -45690 | 0.47 | 1611 | 19 | 51.2 | -152920 | 0.51 | -94.9 | 153120 | False | False |
| OFF: RSI, PCR, SENSEX-PCRmin, ATR | ON: LTP-EMA20, EMAgap, GapUpRSI | 801 | -64150 | 0.55 | 405 | -43800 | 0.46 | 409 | 4 | -45690 | 0.47 | 1615 | 19 | 51.1 | -153640 | 0.5 | -95.1 | 153840 | False | False |
| OFF: RSI, GapUpRSI, ATR | ON: LTP-EMA20, EMAgap, PCR, SENSEX-PCRmin | 901 | -74940 | 0.54 | 471 | -46820 | 0.49 | 259 | 4 | -31990 | 0.44 | 1631 | 19 | 51.2 | -153750 | 0.51 | -94.3 | 154350 | False | False |
| OFF: RSI, GapUpRSI, SENSEX-PCRmin, ATR | ON: LTP-EMA20, EMAgap, PCR | 903 | -75100 | 0.54 | 473 | -47380 | 0.49 | 259 | 4 | -31990 | 0.44 | 1635 | 19 | 51.2 | -154470 | 0.5 | -94.5 | 155070 | False | False |
| OFF: LTP-EMA20, RSI | ON: EMAgap, GapUpRSI, PCR, SENSEX-PCRmin, ATR | 920 | -68960 | 0.58 | 463 | -52080 | 0.44 | 343 | 4 | -43980 | 0.42 | 1726 | 19 | 51.0 | -165020 | 0.5 | -95.6 | 165620 | False | False |
| OFF: LTP-EMA20, RSI, SENSEX-PCRmin | ON: EMAgap, GapUpRSI, PCR, ATR | 923 | -69400 | 0.58 | 468 | -53080 | 0.44 | 348 | 4 | -44980 | 0.41 | 1739 | 19 | 50.8 | -167460 | 0.5 | -96.3 | 168060 | False | False |
| OFF: RSI, GapUpRSI, PCR | ON: LTP-EMA20, EMAgap, SENSEX-PCRmin, ATR | 910 | -77440 | 0.53 | 520 | -46110 | 0.52 | 458 | 4 | -47950 | 0.47 | 1888 | 19 | 51.5 | -171500 | 0.51 | -90.8 | 172100 | False | False |
| OFF: EMAgap, RSI | ON: LTP-EMA20, GapUpRSI, PCR, SENSEX-PCRmin, ATR | 937 | -74280 | 0.57 | 458 | -60320 | 0.42 | 287 | 4 | -37270 | 0.43 | 1682 | 19 | 50.1 | -171870 | 0.5 | -102.2 | 172470 | False | False |
| OFF: LTP-EMA20, RSI, ATR | ON: EMAgap, GapUpRSI, PCR, SENSEX-PCRmin | 973 | -72620 | 0.57 | 484 | -54140 | 0.44 | 363 | 4 | -45240 | 0.43 | 1820 | 19 | 51.3 | -172000 | 0.5 | -94.5 | 172600 | False | False |
| OFF: RSI, GapUpRSI, PCR, SENSEX-PCRmin | ON: LTP-EMA20, EMAgap, ATR | 912 | -77600 | 0.53 | 522 | -46670 | 0.52 | 458 | 4 | -47950 | 0.47 | 1892 | 19 | 51.5 | -172220 | 0.51 | -91.0 | 172820 | False | False |
| OFF: EMAgap, RSI, SENSEX-PCRmin | ON: LTP-EMA20, GapUpRSI, PCR, ATR | 939 | -74440 | 0.57 | 461 | -60760 | 0.42 | 287 | 4 | -37270 | 0.43 | 1687 | 19 | 50.1 | -172470 | 0.5 | -102.2 | 173070 | False | False |
| OFF: LTP-EMA20, RSI, SENSEX-PCRmin, ATR | ON: EMAgap, GapUpRSI, PCR | 976 | -73060 | 0.57 | 491 | -55300 | 0.44 | 368 | 4 | -46240 | 0.42 | 1835 | 19 | 51.1 | -174600 | 0.5 | -95.1 | 175200 | False | False |
| OFF: LTP-EMA20, RSI, PCR | ON: EMAgap, GapUpRSI, SENSEX-PCRmin, ATR | 940 | -69840 | 0.58 | 516 | -52930 | 0.47 | 520 | 4 | -54080 | 0.49 | 1976 | 19 | 51.8 | -176850 | 0.52 | -89.5 | 177450 | False | False |
| OFF: RSI, GapUpRSI, PCR, ATR | ON: LTP-EMA20, EMAgap, SENSEX-PCRmin | 971 | -80140 | 0.54 | 542 | -48850 | 0.51 | 481 | 4 | -49610 | 0.48 | 1994 | 19 | 51.8 | -178600 | 0.52 | -89.6 | 179200 | False | False |
| OFF: LTP-EMA20, RSI, PCR, SENSEX-PCRmin | ON: EMAgap, GapUpRSI, ATR | 943 | -70280 | 0.58 | 521 | -53930 | 0.46 | 525 | 4 | -55080 | 0.48 | 1989 | 19 | 51.6 | -179290 | 0.52 | -90.1 | 179890 | False | False |
| OFF: RSI, GapUpRSI, PCR, SENSEX-PCRmin, ATR | ON: LTP-EMA20, EMAgap | 973 | -80300 | 0.54 | 544 | -49410 | 0.51 | 481 | 4 | -49610 | 0.48 | 1998 | 19 | 51.8 | -179320 | 0.51 | -89.7 | 179920 | False | False |
| OFF: LTP-EMA20, RSI, GapUpRSI | ON: EMAgap, PCR, SENSEX-PCRmin, ATR | 1067 | -80710 | 0.57 | 606 | -56370 | 0.51 | 353 | 4 | -43380 | 0.43 | 2026 | 19 | 51.9 | -180460 | 0.52 | -89.1 | 181260 | False | False |
| OFF: LTP-EMA20, RSI, GapUpRSI, SENSEX-PCRmin | ON: EMAgap, PCR, ATR | 1070 | -81150 | 0.57 | 611 | -57370 | 0.5 | 358 | 4 | -44380 | 0.43 | 2039 | 19 | 51.7 | -182900 | 0.52 | -89.7 | 183700 | False | False |
| OFF: EMAgap, RSI, PCR | ON: LTP-EMA20, GapUpRSI, SENSEX-PCRmin, ATR | 960 | -75720 | 0.57 | 511 | -60550 | 0.45 | 454 | 4 | -47260 | 0.5 | 1925 | 19 | 51.0 | -183530 | 0.52 | -95.3 | 184130 | False | False |
| OFF: EMAgap, RSI, ATR | ON: LTP-EMA20, GapUpRSI, PCR, SENSEX-PCRmin | 997 | -81660 | 0.56 | 487 | -62440 | 0.42 | 306 | 4 | -39550 | 0.43 | 1790 | 19 | 50.0 | -183650 | 0.49 | -102.6 | 184250 | False | False |
| OFF: EMAgap, RSI, PCR, SENSEX-PCRmin | ON: LTP-EMA20, GapUpRSI, ATR | 962 | -75880 | 0.57 | 514 | -60990 | 0.45 | 454 | 4 | -47260 | 0.5 | 1930 | 19 | 50.9 | -184130 | 0.52 | -95.4 | 184730 | False | False |
| OFF: LTP-EMA20, RSI, PCR, ATR | ON: EMAgap, GapUpRSI, SENSEX-PCRmin | 1006 | -72740 | 0.58 | 539 | -55550 | 0.46 | 547 | 4 | -56100 | 0.49 | 2092 | 19 | 52.1 | -184390 | 0.52 | -88.1 | 184990 | False | False |
| OFF: EMAgap, RSI, SENSEX-PCRmin, ATR | ON: LTP-EMA20, GapUpRSI, PCR | 999 | -81820 | 0.56 | 493 | -63320 | 0.42 | 306 | 4 | -39550 | 0.43 | 1798 | 19 | 50.0 | -184690 | 0.49 | -102.7 | 185290 | False | False |
| OFF: EMAgap, RSI, GapUpRSI | ON: LTP-EMA20, PCR, SENSEX-PCRmin, ATR | 1074 | -85870 | 0.56 | 575 | -63550 | 0.47 | 296 | 4 | -37270 | 0.44 | 1945 | 19 | 50.9 | -186690 | 0.51 | -96.0 | 187690 | False | False |
| OFF: LTP-EMA20, RSI, PCR, SENSEX-PCRmin, ATR | ON: EMAgap, GapUpRSI | 1009 | -73180 | 0.58 | 546 | -56710 | 0.46 | 552 | 4 | -57100 | 0.49 | 2107 | 19 | 51.9 | -186990 | 0.52 | -88.7 | 187590 | False | False |
| OFF: EMAgap, RSI, GapUpRSI, SENSEX-PCRmin | ON: LTP-EMA20, PCR, ATR | 1076 | -86030 | 0.56 | 578 | -63990 | 0.47 | 296 | 4 | -37270 | 0.44 | 1950 | 19 | 50.9 | -187290 | 0.51 | -96.0 | 188290 | False | False |
| OFF: LTP-EMA20, RSI, GapUpRSI, ATR | ON: EMAgap, PCR, SENSEX-PCRmin | 1128 | -84210 | 0.57 | 628 | -58710 | 0.5 | 373 | 4 | -44640 | 0.44 | 2129 | 19 | 52.2 | -187560 | 0.52 | -88.1 | 188360 | False | False |
| OFF: LTP-EMA20, RSI, GapUpRSI, SENSEX-PCRmin, ATR | ON: EMAgap, PCR | 1131 | -84650 | 0.56 | 635 | -59870 | 0.5 | 378 | 4 | -45640 | 0.44 | 2144 | 19 | 52.0 | -190160 | 0.52 | -88.7 | 190960 | False | False |
| OFF: EMAgap, RSI, PCR, ATR | ON: LTP-EMA20, GapUpRSI, SENSEX-PCRmin | 1038 | -83340 | 0.56 | 542 | -63230 | 0.45 | 480 | 4 | -50300 | 0.5 | 2060 | 19 | 50.9 | -196870 | 0.51 | -95.6 | 197470 | False | False |
| OFF: EMAgap, RSI, PCR, SENSEX-PCRmin, ATR | ON: LTP-EMA20, GapUpRSI | 1040 | -83500 | 0.56 | 548 | -64110 | 0.45 | 480 | 4 | -50300 | 0.5 | 2068 | 19 | 50.9 | -197910 | 0.51 | -95.7 | 198510 | False | False |
| OFF: EMAgap, RSI, GapUpRSI, ATR | ON: LTP-EMA20, PCR, SENSEX-PCRmin | 1140 | -93330 | 0.55 | 606 | -66770 | 0.47 | 315 | 4 | -39550 | 0.44 | 2061 | 19 | 50.8 | -199650 | 0.5 | -96.9 | 200650 | False | False |
| OFF: EMAgap, RSI, GapUpRSI, SENSEX-PCRmin, ATR | ON: LTP-EMA20, PCR | 1142 | -93490 | 0.55 | 612 | -67650 | 0.47 | 315 | 4 | -39550 | 0.44 | 2069 | 19 | 50.7 | -200690 | 0.5 | -97.0 | 201690 | False | False |
| OFF: LTP-EMA20, RSI, GapUpRSI, PCR | ON: EMAgap, SENSEX-PCRmin, ATR | 1127 | -86670 | 0.56 | 679 | -57620 | 0.53 | 601 | 4 | -58520 | 0.49 | 2407 | 19 | 52.5 | -202810 | 0.53 | -84.3 | 203610 | False | False |
| OFF: LTP-EMA20, RSI, GapUpRSI, PCR, SENSEX-PCRmin | ON: EMAgap, ATR | 1130 | -87110 | 0.56 | 684 | -58620 | 0.52 | 606 | 4 | -59520 | 0.49 | 2420 | 19 | 52.3 | -205250 | 0.53 | -84.8 | 206050 | False | False |
| OFF: EMAgap, RSI, GapUpRSI, PCR | ON: LTP-EMA20, SENSEX-PCRmin, ATR | 1136 | -92070 | 0.56 | 648 | -64180 | 0.5 | 527 | 4 | -51580 | 0.5 | 2311 | 19 | 51.6 | -207830 | 0.53 | -89.9 | 209130 | False | False |
| OFF: EMAgap, RSI, GapUpRSI, PCR, SENSEX-PCRmin | ON: LTP-EMA20, ATR | 1138 | -92230 | 0.56 | 651 | -64620 | 0.5 | 527 | 4 | -51580 | 0.5 | 2316 | 19 | 51.6 | -208430 | 0.53 | -90.0 | 209730 | False | False |
| OFF: LTP-EMA20, RSI, GapUpRSI, PCR, ATR | ON: EMAgap, SENSEX-PCRmin | 1201 | -89410 | 0.57 | 703 | -60520 | 0.52 | 628 | 4 | -60540 | 0.5 | 2532 | 19 | 52.8 | -210470 | 0.54 | -83.1 | 211270 | False | False |
| OFF: LTP-EMA20, RSI, GapUpRSI, PCR, SENSEX-PCRmin, ATR | ON: EMAgap | 1204 | -89850 | 0.56 | 710 | -61680 | 0.52 | 633 | 4 | -61540 | 0.49 | 2547 | 19 | 52.6 | -213070 | 0.53 | -83.7 | 213870 | False | False |
| OFF: EMAgap, RSI, GapUpRSI, PCR, ATR | ON: LTP-EMA20, SENSEX-PCRmin | 1220 | -99770 | 0.55 | 681 | -67960 | 0.5 | 553 | 4 | -54620 | 0.5 | 2454 | 19 | 51.5 | -222350 | 0.52 | -90.6 | 223650 | False | False |
| OFF: EMAgap, RSI, GapUpRSI, PCR, SENSEX-PCRmin, ATR | ON: LTP-EMA20 | 1222 | -99930 | 0.55 | 687 | -68840 | 0.49 | 553 | 4 | -54620 | 0.5 | 2462 | 19 | 51.5 | -223390 | 0.52 | -90.7 | 224690 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI | ON: GapUpRSI, PCR, SENSEX-PCRmin, ATR | 1225 | -94170 | 0.58 | 662 | -76380 | 0.45 | 456 | 4 | -61170 | 0.41 | 2343 | 19 | 50.6 | -231720 | 0.5 | -98.9 | 232520 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, SENSEX-PCRmin | ON: GapUpRSI, PCR, ATR | 1229 | -94890 | 0.58 | 673 | -77860 | 0.45 | 463 | 4 | -62730 | 0.41 | 2365 | 19 | 50.4 | -235480 | 0.5 | -99.6 | 236280 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, PCR | ON: GapUpRSI, SENSEX-PCRmin, ATR | 1251 | -95750 | 0.58 | 722 | -77550 | 0.48 | 649 | 4 | -68670 | 0.49 | 2622 | 19 | 51.4 | -241970 | 0.53 | -92.3 | 242770 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, GapUpRSI | ON: PCR, SENSEX-PCRmin, ATR | 1384 | -105880 | 0.57 | 808 | -78350 | 0.51 | 466 | 4 | -61450 | 0.42 | 2658 | 19 | 51.5 | -245680 | 0.52 | -92.4 | 247080 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, PCR, SENSEX-PCRmin | ON: GapUpRSI, ATR | 1255 | -96470 | 0.58 | 733 | -79030 | 0.47 | 656 | 4 | -70230 | 0.49 | 2644 | 19 | 51.2 | -245730 | 0.52 | -92.9 | 246530 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, ATR | ON: GapUpRSI, PCR, SENSEX-PCRmin | 1308 | -102590 | 0.57 | 693 | -78660 | 0.46 | 481 | 4 | -64910 | 0.41 | 2482 | 19 | 50.6 | -246160 | 0.5 | -99.2 | 246760 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, GapUpRSI, SENSEX-PCRmin | ON: PCR, ATR | 1388 | -106600 | 0.57 | 819 | -79830 | 0.51 | 473 | 4 | -63010 | 0.41 | 2680 | 19 | 51.3 | -249440 | 0.52 | -93.1 | 250840 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, SENSEX-PCRmin, ATR | ON: GapUpRSI, PCR | 1312 | -103310 | 0.56 | 710 | -81020 | 0.45 | 488 | 4 | -66470 | 0.4 | 2510 | 19 | 50.3 | -250800 | 0.49 | -99.9 | 251400 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, PCR, ATR | ON: GapUpRSI, SENSEX-PCRmin | 1352 | -104520 | 0.57 | 755 | -80390 | 0.48 | 681 | 4 | -73170 | 0.49 | 2788 | 19 | 51.3 | -258080 | 0.52 | -92.6 | 258680 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, GapUpRSI, ATR | ON: PCR, SENSEX-PCRmin | 1476 | -114420 | 0.56 | 841 | -81730 | 0.51 | 491 | 4 | -65190 | 0.41 | 2808 | 19 | 51.4 | -261340 | 0.52 | -93.1 | 262740 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, PCR, SENSEX-PCRmin, ATR | ON: GapUpRSI | 1356 | -105240 | 0.57 | 772 | -82750 | 0.47 | 688 | 4 | -74730 | 0.48 | 2816 | 19 | 51.1 | -262720 | 0.52 | -93.3 | 263320 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, GapUpRSI, PCR | ON: SENSEX-PCRmin, ATR | 1451 | -112420 | 0.57 | 888 | -79920 | 0.53 | 731 | 4 | -73510 | 0.5 | 3070 | 19 | 52.2 | -265850 | 0.54 | -86.6 | 267250 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, GapUpRSI, SENSEX-PCRmin, ATR | ON: PCR | 1480 | -115140 | 0.56 | 858 | -84090 | 0.51 | 498 | 4 | -66750 | 0.41 | 2836 | 19 | 51.2 | -265980 | 0.51 | -93.8 | 267380 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, GapUpRSI, PCR, SENSEX-PCRmin | ON: ATR | 1455 | -113140 | 0.57 | 899 | -81400 | 0.53 | 738 | 4 | -75070 | 0.49 | 3092 | 19 | 52.0 | -269610 | 0.54 | -87.2 | 271010 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, GapUpRSI, PCR, ATR | ON: SENSEX-PCRmin | 1561 | -121310 | 0.56 | 923 | -83860 | 0.53 | 763 | 4 | -78010 | 0.49 | 3247 | 19 | 52.1 | -283180 | 0.53 | -87.2 | 284580 | False | False |

## RSI band (normal open)

| Cell | train_N | train_Net | train_PF | val_N | val_Net | val_PF | hold_N | hold_Days | hold_Net | hold_PF | all_N | all_Days | all_TgtPct | all_Net | all_PF | all_Exp | all_MaxDD | BeatsLive_all3 | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| RSI5m 35-38 | 246 | -16560 | 0.61 | 95 | -8460 | 0.53 | 36 | 3 | 180 | 1.04 | 377 | 18 | 57.0 | -24840 | 0.62 | -65.9 | 25720 | True | False |
| RSI5m 32-35 | 264 | -20480 | 0.57 | 79 | -3020 | 0.78 | 45 | 3 | -5230 | 0.45 | 388 | 18 | 54.3 | -28730 | 0.59 | -74.0 | 28930 | True | False |
| RSI5m 35-40 | 302 | -21990 | 0.58 | 116 | -12010 | 0.48 | 49 | 4 | -1620 | 0.8 | 467 | 19 | 54.9 | -35620 | 0.58 | -76.3 | 36740 | True | False |
| RSI5m 30-35 | 321 | -22570 | 0.6 | 93 | -6290 | 0.65 | 66 | 3 | -8720 | 0.42 | 480 | 18 | 53.6 | -37580 | 0.58 | -78.3 | 37780 | True | False |
| RSI5m 32-38 | 353 | -27870 | 0.56 | 127 | -8230 | 0.64 | 71 | 3 | -6280 | 0.55 | 551 | 18 | 54.4 | -42380 | 0.58 | -76.9 | 42860 | True | False |
| RSI5m 28-35 | 380 | -27290 | 0.59 | 114 | -13470 | 0.49 | 74 | 3 | -10380 | 0.4 | 568 | 18 | 52.3 | -51140 | 0.54 | -90.0 | 51340 | True | False |
| RSI5m 30-38 | 410 | -30610 | 0.58 | 140 | -10680 | 0.59 | 94 | 3 | -10110 | 0.5 | 644 | 18 | 53.9 | -51400 | 0.57 | -79.8 | 51800 | True | False |
| RSI5m 35-42 | 347 | -26800 | 0.57 | 160 | -18900 | 0.43 | 70 | 4 | -5700 | 0.58 | 577 | 19 | 52.6 | -51400 | 0.53 | -89.1 | 52600 | False | False |
| RSI5m 32-40 | 407 | -32860 | 0.55 | 148 | -11780 | 0.59 | 87 | 4 | -8870 | 0.51 | 642 | 19 | 53.2 | -53510 | 0.55 | -83.3 | 53990 | True | False |
| RSI5m 25-35 | 427 | -33310 | 0.58 | 129 | -15700 | 0.46 | 84 | 3 | -13160 | 0.38 | 640 | 18 | 51.0 | -62170 | 0.52 | -97.1 | 62370 | False | False |
| RSI5m 30-40 | 464 | -35600 | 0.57 | 161 | -14230 | 0.55 | 111 | 4 | -13520 | 0.47 | 736 | 19 | 52.8 | -63350 | 0.55 | -86.1 | 63750 | False | False |
| RSI5m 35-45 | 384 | -29200 | 0.57 | 220 | -26430 | 0.42 | 104 | 4 | -11110 | 0.49 | 708 | 19 | 51.7 | -66740 | 0.51 | -94.3 | 66340 | False | False |
| RSI5m 20-35 | 451 | -37710 | 0.56 | 130 | -15980 | 0.46 | 87 | 3 | -13600 | 0.37 | 668 | 18 | 50.6 | -67290 | 0.51 | -100.7 | 67490 | False | False |
| RSI5m 0-35 | 466 | -39250 | 0.56 | 130 | -15980 | 0.46 | 87 | 3 | -13600 | 0.37 | 683 | 18 | 50.7 | -68830 | 0.51 | -100.8 | 69030 | False | False |
| RSI5m 32-42 | 451 | -37270 | 0.54 | 192 | -18670 | 0.52 | 108 | 4 | -12950 | 0.45 | 751 | 19 | 51.8 | -68890 | 0.52 | -91.7 | 69770 | False | False |
| RSI5m 35-50 | 407 | -30340 | 0.58 | 250 | -28880 | 0.43 | 136 | 4 | -13970 | 0.49 | 793 | 19 | 51.8 | -73190 | 0.51 | -92.3 | 73390 | False | False |
| RSI5m 25-38 | 511 | -39490 | 0.57 | 176 | -19860 | 0.47 | 112 | 3 | -14550 | 0.45 | 799 | 18 | 51.9 | -73900 | 0.53 | -92.5 | 74300 | False | False |
| RSI5m 28-40 | 519 | -39930 | 0.57 | 182 | -20530 | 0.48 | 119 | 4 | -15180 | 0.45 | 820 | 19 | 52.1 | -75640 | 0.53 | -92.2 | 76040 | False | False |
| RSI5m 20-38 | 535 | -43890 | 0.56 | 177 | -19490 | 0.48 | 115 | 3 | -14990 | 0.45 | 827 | 18 | 51.6 | -78370 | 0.52 | -94.8 | 78770 | False | False |
| RSI5m 30-42 | 507 | -40130 | 0.56 | 205 | -21120 | 0.5 | 132 | 4 | -17600 | 0.43 | 844 | 19 | 51.5 | -78850 | 0.52 | -93.4 | 79650 | False | False |
| RSI5m 0-38 | 550 | -45430 | 0.56 | 177 | -19490 | 0.48 | 115 | 3 | -14990 | 0.45 | 842 | 18 | 51.7 | -79910 | 0.52 | -94.9 | 80310 | False | False |
| RSI5m 32-45 | 487 | -39270 | 0.55 | 252 | -26200 | 0.49 | 141 | 4 | -17540 | 0.44 | 880 | 19 | 51.3 | -83010 | 0.51 | -94.3 | 82610 | False | False |
| RSI5m 25-40 | 565 | -44480 | 0.57 | 197 | -23410 | 0.46 | 129 | 4 | -17960 | 0.43 | 891 | 19 | 51.2 | -85850 | 0.52 | -96.4 | 86250 | False | False |
| RSI5m 32-50 | 509 | -40010 | 0.56 | 282 | -28650 | 0.49 | 173 | 4 | -20400 | 0.45 | 964 | 19 | 51.5 | -89060 | 0.52 | -92.4 | 89260 | False | False |
| RSI5m 20-40 | 589 | -48880 | 0.55 | 198 | -23040 | 0.46 | 132 | 4 | -18400 | 0.43 | 919 | 19 | 51.0 | -90320 | 0.51 | -98.3 | 90720 | False | False |
| RSI5m 28-42 | 562 | -44460 | 0.56 | 226 | -27420 | 0.45 | 140 | 4 | -19260 | 0.42 | 928 | 19 | 51.0 | -91140 | 0.51 | -98.2 | 91940 | False | False |
| RSI5m 0-40 | 604 | -50420 | 0.55 | 198 | -23040 | 0.46 | 132 | 4 | -18400 | 0.43 | 934 | 19 | 51.1 | -91860 | 0.51 | -98.4 | 92260 | False | False |
| RSI5m 30-45 | 543 | -42130 | 0.57 | 265 | -28650 | 0.47 | 165 | 4 | -22190 | 0.42 | 973 | 19 | 51.2 | -92970 | 0.51 | -95.5 | 92570 | False | False |
| RSI5m 30-50 | 563 | -42670 | 0.57 | 295 | -31100 | 0.48 | 197 | 4 | -25050 | 0.43 | 1055 | 19 | 51.3 | -98820 | 0.51 | -93.7 | 99020 | False | False |
| RSI5m 25-42 | 608 | -49010 | 0.56 | 241 | -30300 | 0.43 | 150 | 4 | -22040 | 0.4 | 999 | 19 | 50.3 | -101350 | 0.5 | -101.5 | 102150 | False | False |
| RSI5m 28-45 | 598 | -46460 | 0.57 | 286 | -34950 | 0.44 | 173 | 4 | -23850 | 0.41 | 1057 | 19 | 50.7 | -105260 | 0.5 | -99.6 | 104860 | False | False |
| RSI5m 20-42 | 632 | -53410 | 0.55 | 242 | -29930 | 0.43 | 153 | 4 | -22480 | 0.4 | 1027 | 19 | 50.2 | -105820 | 0.49 | -103.0 | 106620 | False | False |
| RSI5m 0-42 | 647 | -54950 | 0.55 | 242 | -29930 | 0.43 | 153 | 4 | -22480 | 0.4 | 1042 | 19 | 50.2 | -107360 | 0.49 | -103.0 | 108160 | False | False |
| RSI5m 28-50 | 618 | -47000 | 0.57 | 316 | -37400 | 0.44 | 205 | 4 | -26710 | 0.42 | 1139 | 19 | 50.9 | -111110 | 0.5 | -97.6 | 111310 | False | False |
| RSI5m 25-45 | 644 | -51010 | 0.57 | 301 | -37830 | 0.42 | 183 | 4 | -26630 | 0.4 | 1128 | 19 | 50.1 | -115470 | 0.49 | -102.4 | 115070 | False | False |
| RSI5m 20-45 | 668 | -55410 | 0.55 | 302 | -37460 | 0.43 | 186 | 4 | -27070 | 0.4 | 1156 | 19 | 50.0 | -119940 | 0.49 | -103.8 | 119540 | False | False |
| RSI5m 25-50 | 664 | -51550 | 0.57 | 331 | -40280 | 0.43 | 215 | 4 | -29490 | 0.41 | 1210 | 19 | 50.3 | -121320 | 0.5 | -100.3 | 121520 | False | False |
| RSI5m 0-45 | 683 | -56950 | 0.55 | 302 | -37460 | 0.43 | 186 | 4 | -27070 | 0.4 | 1171 | 19 | 50.0 | -121480 | 0.49 | -103.7 | 121080 | False | False |
| RSI5m 20-50 | 688 | -55950 | 0.56 | 332 | -39910 | 0.43 | 218 | 4 | -29930 | 0.41 | 1238 | 19 | 50.2 | -125790 | 0.49 | -101.6 | 125990 | False | False |
| RSI5m 0-50 | 703 | -57490 | 0.56 | 332 | -39910 | 0.43 | 218 | 4 | -29930 | 0.41 | 1253 | 19 | 50.2 | -127330 | 0.49 | -101.6 | 127530 | False | False |
| RSI filter off (gap-up band kept) | 724 | -60010 | 0.55 | 333 | -39710 | 0.44 | 234 | 4 | -31970 | 0.4 | 1291 | 19 | 50.1 | -131690 | 0.49 | -102.0 | 131890 | False | False |

## RSI band (gap-up open)

| Cell | train_N | train_Net | train_PF | val_N | val_Net | val_PF | hold_N | hold_Days | hold_Net | hold_PF | all_N | all_Days | all_TgtPct | all_Net | all_PF | all_Exp | all_MaxDD | BeatsLive_all3 | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Gap-up RSI5m 40-50 | 445 | -33240 | 0.58 | 159 | -16820 | 0.51 | 102 | 3 | -11770 | 0.48 | 706 | 18 | 52.9 | -61830 | 0.54 | -87.6 | 62830 | False | False |
| Gap-up RSI5m 40-70 | 466 | -34820 | 0.58 | 161 | -16980 | 0.5 | 102 | 3 | -11770 | 0.48 | 729 | 18 | 53.0 | -63570 | 0.54 | -87.2 | 63970 | False | False |
| Gap-up override off (normal band on gap-up) | 418 | -37160 | 0.52 | 191 | -18480 | 0.51 | 107 | 3 | -11010 | 0.52 | 716 | 16 | 51.6 | -66650 | 0.52 | -93.1 | 66550 | False | False |
| Gap-up RSI5m 28-38 | 418 | -37160 | 0.52 | 191 | -18480 | 0.51 | 107 | 3 | -11010 | 0.52 | 716 | 16 | 51.6 | -66650 | 0.52 | -93.1 | 66550 | False | False |
| Gap-up RSI5m 35-60 | 520 | -42100 | 0.55 | 198 | -18980 | 0.52 | 103 | 3 | -11290 | 0.5 | 821 | 18 | 52.6 | -72370 | 0.54 | -88.1 | 72270 | False | False |
| Gap-up RSI5m 30-50 | 535 | -42690 | 0.55 | 233 | -21410 | 0.53 | 105 | 3 | -10890 | 0.52 | 873 | 18 | 52.9 | -74990 | 0.54 | -85.9 | 75090 | False | False |

## PCR band

| Cell | train_N | train_Net | train_PF | val_N | val_Net | val_PF | hold_N | hold_Days | hold_Net | hold_PF | all_N | all_Days | all_TgtPct | all_Net | all_PF | all_Exp | all_MaxDD | BeatsLive_all3 | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| PCR 0.95-1.0 | 59 | -4870 | 0.63 | 14 | -3290 | 0.3 | 16 | 2 | -1000 | 0.62 | 89 | 13 | 50.6 | -9160 | 0.55 | -102.9 | 8760 | True | False |
| PCR 1.1-1.2 | 151 | -9460 | 0.64 | 35 | -1300 | 0.75 | 13 | 2 | -1240 | 0.59 | 199 | 15 | 54.4 | -12000 | 0.65 | -60.3 | 12320 | True | False |
| PCR 1.1-1.3 | 216 | -10590 | 0.69 | 59 | -2100 | 0.74 | 19 | 3 | -760 | 0.73 | 294 | 16 | 58.5 | -13450 | 0.7 | -45.7 | 13850 | True | False |
| PCR 0.9-1.0 | 97 | -8160 | 0.61 | 33 | -4290 | 0.53 | 27 | 2 | -4280 | 0.37 | 157 | 14 | 50.7 | -16730 | 0.55 | -106.6 | 16330 | True | False |
| PCR 1.1-1.4 | 239 | -10630 | 0.71 | 77 | -6320 | 0.53 | 36 | 3 | -730 | 0.88 | 352 | 16 | 59.0 | -17680 | 0.68 | -50.2 | 18160 | True | False |
| PCR 1.05-1.2 | 219 | -15120 | 0.6 | 55 | -3500 | 0.63 | 23 | 3 | -3160 | 0.46 | 297 | 17 | 51.9 | -21780 | 0.59 | -73.3 | 21980 | True | False |
| PCR 1.1-none | 253 | -12150 | 0.69 | 99 | -7710 | 0.54 | 96 | 3 | -2550 | 0.84 | 448 | 16 | 59.0 | -22410 | 0.69 | -50.0 | 22890 | True | False |
| PCR 1.1-1.5 | 250 | -12190 | 0.69 | 86 | -6760 | 0.55 | 61 | 3 | -4430 | 0.64 | 397 | 16 | 58.1 | -23380 | 0.65 | -58.9 | 23860 | True | False |
| PCR 1.05-1.3 | 280 | -16770 | 0.64 | 79 | -4300 | 0.65 | 29 | 3 | -2680 | 0.52 | 388 | 17 | 55.4 | -23750 | 0.63 | -61.2 | 24150 | True | False |
| PCR 0.8-1.0 | 150 | -13610 | 0.57 | 47 | -5900 | 0.56 | 42 | 2 | -7060 | 0.37 | 239 | 14 | 49.5 | -26570 | 0.53 | -111.2 | 26170 | True | False |
| PCR 1.05-1.4 | 299 | -16770 | 0.65 | 97 | -8520 | 0.52 | 46 | 3 | -2650 | 0.7 | 442 | 17 | 56.0 | -27940 | 0.62 | -63.2 | 28340 | True | False |
| PCR 0.7-1.0 | 163 | -15540 | 0.54 | 51 | -6800 | 0.53 | 53 | 2 | -8060 | 0.38 | 267 | 14 | 48.6 | -30400 | 0.51 | -113.9 | 30000 | True | False |
| PCR none-1.0 | 163 | -15540 | 0.54 | 51 | -6800 | 0.53 | 58 | 2 | -9460 | 0.35 | 272 | 14 | 48.0 | -31800 | 0.5 | -116.9 | 31400 | True | False |
| PCR 1.05-none | 312 | -18010 | 0.64 | 119 | -9910 | 0.53 | 106 | 3 | -4470 | 0.76 | 537 | 17 | 56.6 | -32390 | 0.64 | -60.3 | 32790 | True | False |
| PCR 1.05-1.5 | 309 | -18050 | 0.64 | 106 | -8960 | 0.54 | 71 | 3 | -6350 | 0.58 | 486 | 17 | 55.6 | -33360 | 0.6 | -68.6 | 33760 | True | False |
| PCR 0.95-1.2 | 311 | -23740 | 0.59 | 92 | -8630 | 0.52 | 40 | 3 | -4580 | 0.49 | 443 | 18 | 52.8 | -36950 | 0.56 | -83.4 | 37150 | True | False |
| PCR 0.95-1.3 | 369 | -25750 | 0.61 | 113 | -9390 | 0.54 | 46 | 3 | -4500 | 0.5 | 528 | 18 | 54.7 | -39640 | 0.58 | -75.1 | 40040 | True | False |
| PCR 0.9-1.2 | 343 | -26120 | 0.59 | 106 | -8810 | 0.58 | 49 | 3 | -7520 | 0.39 | 498 | 18 | 52.6 | -42450 | 0.56 | -85.2 | 42650 | True | False |
| PCR 0.95-1.4 | 388 | -25750 | 0.62 | 131 | -13610 | 0.48 | 63 | 3 | -4470 | 0.63 | 582 | 18 | 55.2 | -43830 | 0.58 | -75.3 | 44230 | True | False |
| PCR 0.9-1.3 | 398 | -28850 | 0.6 | 127 | -9570 | 0.6 | 55 | 3 | -7440 | 0.4 | 580 | 18 | 54.1 | -45860 | 0.57 | -79.1 | 46260 | True | False |
| PCR 0.95-none | 401 | -26990 | 0.61 | 153 | -15000 | 0.49 | 123 | 3 | -6290 | 0.71 | 677 | 18 | 55.8 | -48280 | 0.6 | -71.3 | 48680 | True | False |
| PCR 0.95-1.5 | 398 | -27030 | 0.61 | 140 | -14050 | 0.49 | 88 | 3 | -8170 | 0.56 | 626 | 18 | 55.0 | -49250 | 0.57 | -78.7 | 49650 | True | False |
| PCR 0.9-1.4 | 417 | -28850 | 0.61 | 145 | -13790 | 0.52 | 72 | 3 | -7410 | 0.52 | 634 | 18 | 54.6 | -50050 | 0.57 | -78.9 | 50450 | True | False |
| PCR 0.8-1.2 | 381 | -31260 | 0.56 | 118 | -11100 | 0.56 | 63 | 3 | -9480 | 0.4 | 562 | 18 | 51.5 | -51840 | 0.54 | -92.2 | 52040 | True | False |
| PCR 0.8-1.3 | 435 | -33170 | 0.58 | 139 | -11860 | 0.57 | 69 | 3 | -9400 | 0.41 | 643 | 18 | 53.1 | -54430 | 0.56 | -84.7 | 54830 | True | False |
| PCR 0.9-none | 430 | -30090 | 0.6 | 167 | -15180 | 0.53 | 132 | 3 | -9230 | 0.63 | 729 | 18 | 55.3 | -54500 | 0.59 | -74.8 | 54900 | True | False |
| PCR 0.9-1.5 | 427 | -30130 | 0.6 | 154 | -14230 | 0.53 | 97 | 3 | -11110 | 0.49 | 678 | 18 | 54.5 | -55470 | 0.57 | -81.8 | 55870 | True | False |
| PCR 0.7-1.2 | 392 | -33030 | 0.55 | 122 | -12000 | 0.54 | 74 | 3 | -10480 | 0.41 | 588 | 18 | 51.0 | -55510 | 0.53 | -94.4 | 55710 | True | False |
| PCR none-1.2 | 392 | -33030 | 0.55 | 122 | -12000 | 0.54 | 79 | 3 | -11880 | 0.39 | 593 | 18 | 50.7 | -56910 | 0.52 | -96.0 | 57110 | False | False |
| PCR 0.7-1.3 | 446 | -34940 | 0.57 | 143 | -12760 | 0.56 | 80 | 3 | -10400 | 0.42 | 669 | 18 | 52.6 | -58100 | 0.55 | -86.8 | 58500 | False | False |
| PCR 0.8-1.4 | 454 | -33170 | 0.59 | 157 | -16080 | 0.51 | 86 | 3 | -9370 | 0.51 | 697 | 18 | 53.6 | -58620 | 0.56 | -84.1 | 59020 | True | False |
| PCR none-1.3 | 446 | -34940 | 0.57 | 143 | -12760 | 0.56 | 85 | 3 | -11800 | 0.39 | 674 | 18 | 52.3 | -59500 | 0.54 | -88.3 | 59900 | False | False |
| PCR 0.7-1.4 | 465 | -34940 | 0.58 | 161 | -16980 | 0.5 | 97 | 3 | -10370 | 0.5 | 723 | 18 | 53.2 | -62290 | 0.55 | -86.2 | 62690 | False | False |
| PCR 0.8-none | 467 | -34410 | 0.58 | 179 | -17470 | 0.52 | 146 | 3 | -11190 | 0.61 | 792 | 18 | 54.4 | -63070 | 0.57 | -79.6 | 63470 | False | False |
| SENSEX PCR >= 0.70 off | 466 | -34820 | 0.58 | 161 | -16980 | 0.5 | 102 | 3 | -11770 | 0.48 | 729 | 18 | 53.0 | -63570 | 0.54 | -87.2 | 63970 | False | False |
| PCR 0.8-1.5 | 464 | -34450 | 0.58 | 166 | -16520 | 0.52 | 111 | 3 | -13070 | 0.48 | 741 | 18 | 53.6 | -64040 | 0.55 | -86.4 | 64440 | False | False |
| PCR 0.7-none | 478 | -36180 | 0.57 | 183 | -18370 | 0.51 | 157 | 3 | -12190 | 0.6 | 818 | 18 | 53.9 | -66740 | 0.56 | -81.6 | 67140 | False | False |
| PCR 0.7-1.5 | 475 | -36220 | 0.57 | 170 | -17420 | 0.52 | 122 | 3 | -14070 | 0.48 | 767 | 18 | 53.1 | -67710 | 0.54 | -88.3 | 68110 | False | False |
| PCR none-none | 478 | -36180 | 0.57 | 183 | -18370 | 0.51 | 162 | 3 | -13590 | 0.58 | 823 | 18 | 53.7 | -68140 | 0.56 | -82.8 | 68540 | False | False |
| PCR filter off | 478 | -36180 | 0.57 | 183 | -18370 | 0.51 | 162 | 3 | -13590 | 0.58 | 823 | 18 | 53.7 | -68140 | 0.56 | -82.8 | 68540 | False | False |
| PCR none-1.5 | 475 | -36220 | 0.57 | 170 | -17420 | 0.52 | 127 | 3 | -15470 | 0.46 | 772 | 18 | 52.9 | -69110 | 0.54 | -89.5 | 69510 | False | False |

## ATR min

| Cell | train_N | train_Net | train_PF | val_N | val_Net | val_PF | hold_N | hold_Days | hold_Net | hold_PF | all_N | all_Days | all_TgtPct | all_Net | all_PF | all_Exp | all_MaxDD | BeatsLive_all3 | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| ATR% >= 0.05 | 210 | -17540 | 0.56 | 81 | -11400 | 0.46 | 64 | 3 | -5370 | 0.59 | 355 | 14 | 53.1 | -34310 | 0.54 | -96.6 | 34710 | True | False |
| ATR% >= 0.04 | 367 | -27170 | 0.59 | 123 | -13610 | 0.52 | 80 | 3 | -9690 | 0.48 | 570 | 17 | 53.7 | -50470 | 0.56 | -88.5 | 50870 | True | False |
| ATR% >= 0.02 | 477 | -35700 | 0.57 | 169 | -17220 | 0.51 | 103 | 3 | -11650 | 0.48 | 749 | 18 | 53.1 | -64570 | 0.54 | -86.2 | 64970 | False | False |
| ATR filter off | 486 | -35900 | 0.58 | 171 | -18020 | 0.5 | 104 | 3 | -11170 | 0.5 | 761 | 18 | 53.2 | -65090 | 0.55 | -85.5 | 65490 | False | False |

## EarlyTrendGate

| Cell | train_N | train_Net | train_PF | val_N | val_Net | val_PF | hold_N | hold_Days | hold_Net | hold_PF | all_N | all_Days | all_TgtPct | all_Net | all_PF | all_Exp | all_MaxDD | BeatsLive_all3 | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Require 5m EMA20>EMA50 | 144 | -6860 | 0.69 | 39 | -4120 | 0.52 | 17 | 2 | -3070 | 0.21 | 200 | 14 | 56.9 | -14050 | 0.6 | -70.2 | 14450 | True | False |
| EarlyTrendGate on (block when 5m EMA20>EMA50) | 323 | -28700 | 0.53 | 122 | -12860 | 0.5 | 85 | 3 | -8700 | 0.53 | 530 | 14 | 51.4 | -50260 | 0.52 | -94.8 | 50660 | True | False |
| Require 5m EMA20<EMA50 (5m aligned) | 323 | -28700 | 0.53 | 122 | -12860 | 0.5 | 85 | 3 | -8700 | 0.53 | 530 | 14 | 51.4 | -50260 | 0.52 | -94.8 | 50660 | True | False |

## LTP vs EMA20

| Cell | train_N | train_Net | train_PF | val_N | val_Net | val_PF | hold_N | hold_Days | hold_Net | hold_PF | all_N | all_Days | all_TgtPct | all_Net | all_PF | all_Exp | all_MaxDD | BeatsLive_all3 | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| LTP vs EMA20 off | 560 | -37070 | 0.61 | 206 | -18000 | 0.55 | 130 | 3 | -17000 | 0.43 | 896 | 18 | 53.9 | -72070 | 0.56 | -80.4 | 72870 | False | False |

## EMA gap (live: 10, BANKNIFTY 15)

| Cell | train_N | train_Net | train_PF | val_N | val_Net | val_PF | hold_N | hold_Days | hold_Net | hold_PF | all_N | all_Days | all_TgtPct | all_Net | all_PF | all_Exp | all_MaxDD | BeatsLive_all3 | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| |EMA20-EMA50| > 30 pts (all symbols) | 337 | -22550 | 0.55 | 96 | -10810 | 0.41 | 83 | 3 | -8030 | 0.46 | 516 | 15 | 52.7 | -41390 | 0.51 | -80.2 | 41790 | True | False |
| |EMA20-EMA50| > 20 pts (all symbols) | 390 | -25010 | 0.59 | 132 | -17190 | 0.4 | 93 | 3 | -9960 | 0.48 | 615 | 17 | 52.6 | -52160 | 0.52 | -84.8 | 52560 | False | False |
| |EMA20-EMA50| > 15 pts (all symbols) | 429 | -31490 | 0.56 | 147 | -17310 | 0.45 | 99 | 3 | -13210 | 0.41 | 675 | 18 | 52.3 | -62010 | 0.51 | -91.9 | 62410 | False | False |
| |EMA20-EMA50| > 10 pts (all symbols) | 479 | -36040 | 0.58 | 165 | -17380 | 0.5 | 102 | 3 | -11770 | 0.48 | 746 | 18 | 52.9 | -65190 | 0.54 | -87.4 | 65790 | False | False |
| |EMA20-EMA50| > 5 pts (all symbols) | 510 | -37210 | 0.59 | 178 | -20250 | 0.48 | 104 | 3 | -11740 | 0.48 | 792 | 18 | 52.7 | -69200 | 0.55 | -87.4 | 69800 | False | False |
| EMA gap off | 538 | -39370 | 0.59 | 189 | -19660 | 0.51 | 106 | 3 | -11340 | 0.5 | 833 | 18 | 53.2 | -70370 | 0.56 | -84.5 | 70970 | False | False |

## RSI x PCR (joint)

| Cell | train_N | train_Net | train_PF | val_N | val_Net | val_PF | hold_N | hold_Days | hold_Net | hold_PF | all_N | all_Days | all_TgtPct | all_Net | all_PF | all_Exp | all_MaxDD | BeatsLive_all3 | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| RSI5m 28-35 & PCR none-1.0 | 149 | -12430 | 0.59 | 42 | -7480 | 0.45 | 43 | 2 | -7110 | 0.3 | 234 | 14 | 48.2 | -27020 | 0.5 | -115.5 | 26620 | True | False |
| RSI5m 28-38 & PCR none-1.0 | 163 | -15540 | 0.54 | 51 | -6800 | 0.53 | 58 | 2 | -9460 | 0.35 | 272 | 14 | 48.0 | -31800 | 0.5 | -116.9 | 31400 | True | False |
| RSI5m 28-40 & PCR none-1.0 | 175 | -16790 | 0.54 | 63 | -10120 | 0.46 | 62 | 2 | -8550 | 0.42 | 300 | 14 | 47.9 | -35460 | 0.5 | -118.2 | 35260 | True | False |
| RSI5m 25-38 & PCR none-1.0 | 179 | -21160 | 0.47 | 54 | -7280 | 0.52 | 61 | 2 | -10540 | 0.33 | 294 | 14 | 45.6 | -38980 | 0.45 | -132.6 | 38580 | True | False |
| RSI5m 30-45 & PCR none-1.0 | 154 | -17110 | 0.49 | 115 | -15560 | 0.44 | 60 | 3 | -7330 | 0.46 | 329 | 15 | 46.5 | -40000 | 0.47 | -121.6 | 39600 | True | False |
| RSI5m 28-35 & PCR none-1.2 | 325 | -24910 | 0.59 | 86 | -9600 | 0.53 | 56 | 3 | -10700 | 0.26 | 467 | 18 | 50.3 | -45210 | 0.52 | -96.8 | 45410 | True | False |
| RSI5m 28-35 & PCR 0.8-none | 382 | -26680 | 0.6 | 122 | -14070 | 0.49 | 112 | 3 | -9680 | 0.57 | 616 | 18 | 53.7 | -50430 | 0.57 | -81.9 | 50630 | True | False |
| RSI5m 28-35 & PCR none-1.4 | 380 | -27290 | 0.59 | 114 | -13470 | 0.49 | 74 | 3 | -10380 | 0.4 | 568 | 18 | 52.3 | -51140 | 0.54 | -90.0 | 51340 | True | False |
| RSI5m 28-38 & PCR none-1.2 | 392 | -33030 | 0.55 | 122 | -12000 | 0.54 | 79 | 3 | -11880 | 0.39 | 593 | 18 | 50.7 | -56910 | 0.52 | -96.0 | 57110 | False | False |
| RSI5m 28-38 & PCR 0.8-none | 467 | -34410 | 0.58 | 179 | -17470 | 0.52 | 146 | 3 | -11190 | 0.61 | 792 | 18 | 54.4 | -63070 | 0.57 | -79.6 | 63470 | False | False |
| RSI5m 28-40 & PCR none-1.2 | 434 | -36240 | 0.55 | 140 | -15950 | 0.49 | 91 | 3 | -12570 | 0.42 | 665 | 18 | 50.5 | -64760 | 0.52 | -97.4 | 64960 | False | False |
| RSI5m 25-38 & PCR none-1.2 | 434 | -37820 | 0.54 | 129 | -13840 | 0.51 | 82 | 3 | -14260 | 0.33 | 645 | 18 | 49.6 | -65920 | 0.5 | -102.2 | 66120 | False | False |
| RSI5m 30-45 & PCR none-1.2 | 449 | -37520 | 0.55 | 210 | -22410 | 0.48 | 101 | 4 | -12310 | 0.45 | 760 | 19 | 50.6 | -72240 | 0.52 | -95.1 | 72240 | False | False |
| RSI5m 25-38 & PCR 0.8-none | 513 | -38560 | 0.58 | 197 | -19990 | 0.5 | 160 | 3 | -13950 | 0.58 | 870 | 18 | 53.5 | -72500 | 0.56 | -83.3 | 72900 | False | False |
| RSI5m 25-38 & PCR none-1.4 | 511 | -39490 | 0.57 | 176 | -19860 | 0.47 | 112 | 3 | -14550 | 0.45 | 799 | 18 | 51.9 | -73900 | 0.53 | -92.5 | 74300 | False | False |
| RSI5m 28-40 & PCR none-1.4 | 519 | -39930 | 0.57 | 182 | -20530 | 0.48 | 119 | 4 | -15180 | 0.45 | 820 | 19 | 52.1 | -75640 | 0.53 | -92.2 | 76040 | False | False |
| RSI5m 28-40 & PCR 0.8-none | 522 | -39800 | 0.57 | 209 | -21290 | 0.5 | 180 | 4 | -16600 | 0.56 | 911 | 19 | 53.1 | -77690 | 0.55 | -85.3 | 78090 | False | False |
| RSI5m 30-45 & PCR none-1.4 | 543 | -42130 | 0.57 | 265 | -28650 | 0.47 | 165 | 4 | -22190 | 0.42 | 973 | 19 | 51.2 | -92970 | 0.51 | -95.5 | 92570 | False | False |
| RSI5m 30-45 & PCR 0.8-none | 547 | -41110 | 0.58 | 298 | -32020 | 0.46 | 247 | 4 | -23670 | 0.54 | 1092 | 19 | 52.3 | -96800 | 0.53 | -88.6 | 96400 | False | False |

