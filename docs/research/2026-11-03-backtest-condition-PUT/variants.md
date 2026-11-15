# PUT live-condition variants (2026-09-30..2026-11-03, 23 days, 15-min window)

Simulated only (snapshots + outcomes; no live signals or trades). Live rule, every on/off combination of its conditions, then one condition changed at a time (plus RSI x PCR joint grid).

| Cell | train_N | train_Net | train_PF | val_N | val_Net | val_PF | hold_N | hold_Days | hold_Net | hold_PF | all_N | all_Days | all_TgtPct | all_Net | all_PF | all_Exp | all_MaxDD | BeatsLive_all3 | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| LIVE (current settings) | 540 | -42540 | 0.57 | 145 | -15470 | 0.51 | 127 | 4 | -14460 | 0.43 | 812 | 21 | 52.4 | -72470 | 0.53 | -89.2 | 72870 | False | False |
| All live conditions off (trend rows only) | 1968 | -169370 | 0.53 | 1146 | -101220 | 0.54 | 583 | 5 | -60490 | 0.46 | 3697 | 23 | 51.6 | -331080 | 0.52 | -89.6 | 332480 | False | False |

## Interchange (live conditions on/off)

| Cell | train_N | train_Net | train_PF | val_N | val_Net | val_PF | hold_N | hold_Days | hold_Net | hold_PF | all_N | all_Days | all_TgtPct | all_Net | all_PF | all_Exp | all_MaxDD | BeatsLive_all3 | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| OFF: GapUpRSI | ON: LTP-EMA20, EMAgap, RSI, PCR, SENSEX-PCRmin, ATR | 492 | -44770 | 0.52 | 181 | -16200 | 0.54 | 113 | 4 | -10340 | 0.52 | 786 | 19 | 52.1 | -71310 | 0.52 | -90.7 | 71530 | False | False |
| OFF: GapUpRSI, SENSEX-PCRmin | ON: LTP-EMA20, EMAgap, RSI, PCR, ATR | 493 | -44650 | 0.52 | 181 | -16200 | 0.54 | 114 | 4 | -10620 | 0.51 | 788 | 19 | 52.1 | -71470 | 0.52 | -90.7 | 71410 | False | False |
| OFF: GapUpRSI, ATR | ON: LTP-EMA20, EMAgap, RSI, PCR, SENSEX-PCRmin | 518 | -45530 | 0.52 | 184 | -16880 | 0.53 | 114 | 4 | -9860 | 0.54 | 816 | 19 | 52.4 | -72270 | 0.53 | -88.6 | 72490 | False | False |
| OFF: GapUpRSI, SENSEX-PCRmin, ATR | ON: LTP-EMA20, EMAgap, RSI, PCR | 519 | -45410 | 0.52 | 184 | -16880 | 0.53 | 115 | 4 | -10140 | 0.53 | 818 | 19 | 52.4 | -72430 | 0.53 | -88.5 | 72370 | False | False |
| OFF: SENSEX-PCRmin | ON: LTP-EMA20, EMAgap, RSI, GapUpRSI, PCR, ATR | 541 | -42420 | 0.57 | 145 | -15470 | 0.51 | 128 | 4 | -14740 | 0.43 | 814 | 21 | 52.4 | -72630 | 0.53 | -89.2 | 73030 | False | False |
| OFF: ATR | ON: LTP-EMA20, EMAgap, RSI, GapUpRSI, PCR, SENSEX-PCRmin | 569 | -43740 | 0.57 | 148 | -16150 | 0.5 | 134 | 4 | -14060 | 0.46 | 851 | 21 | 52.7 | -73950 | 0.54 | -86.9 | 74350 | False | False |
| OFF: SENSEX-PCRmin, ATR | ON: LTP-EMA20, EMAgap, RSI, GapUpRSI, PCR | 570 | -43620 | 0.57 | 148 | -16150 | 0.5 | 135 | 4 | -14340 | 0.46 | 853 | 21 | 52.7 | -74110 | 0.54 | -86.9 | 74510 | False | False |
| OFF: EMAgap, GapUpRSI | ON: LTP-EMA20, RSI, PCR, SENSEX-PCRmin, ATR | 510 | -46290 | 0.52 | 185 | -17760 | 0.52 | 113 | 4 | -10340 | 0.52 | 808 | 19 | 52.2 | -74390 | 0.52 | -92.1 | 75410 | False | False |
| OFF: EMAgap, GapUpRSI, SENSEX-PCRmin | ON: LTP-EMA20, RSI, PCR, ATR | 511 | -46170 | 0.52 | 185 | -17760 | 0.52 | 114 | 4 | -10620 | 0.51 | 810 | 19 | 52.2 | -74550 | 0.52 | -92.0 | 75290 | False | False |
| OFF: EMAgap, GapUpRSI, ATR | ON: LTP-EMA20, RSI, PCR, SENSEX-PCRmin | 536 | -47050 | 0.53 | 188 | -18440 | 0.51 | 114 | 4 | -9860 | 0.54 | 838 | 19 | 52.5 | -75350 | 0.53 | -89.9 | 76370 | False | False |
| OFF: EMAgap, GapUpRSI, SENSEX-PCRmin, ATR | ON: LTP-EMA20, RSI, PCR | 537 | -46930 | 0.53 | 188 | -18440 | 0.51 | 115 | 4 | -10140 | 0.53 | 840 | 19 | 52.5 | -75510 | 0.53 | -89.9 | 76250 | False | False |
| OFF: PCR | ON: LTP-EMA20, EMAgap, RSI, GapUpRSI, SENSEX-PCRmin, ATR | 556 | -43420 | 0.57 | 222 | -18840 | 0.57 | 130 | 4 | -14540 | 0.44 | 908 | 21 | 53.2 | -76800 | 0.55 | -84.6 | 77200 | False | False |
| OFF: PCR, SENSEX-PCRmin | ON: LTP-EMA20, EMAgap, RSI, GapUpRSI, ATR | 557 | -43300 | 0.57 | 222 | -18840 | 0.57 | 131 | 4 | -14820 | 0.43 | 910 | 21 | 53.2 | -76960 | 0.55 | -84.6 | 77360 | False | False |
| OFF: PCR, ATR | ON: LTP-EMA20, EMAgap, RSI, GapUpRSI, SENSEX-PCRmin | 591 | -44300 | 0.57 | 228 | -20480 | 0.56 | 141 | 4 | -13340 | 0.5 | 960 | 22 | 53.7 | -78120 | 0.56 | -81.4 | 78520 | False | False |
| OFF: PCR, SENSEX-PCRmin, ATR | ON: LTP-EMA20, EMAgap, RSI, GapUpRSI | 592 | -44180 | 0.57 | 228 | -20480 | 0.56 | 142 | 4 | -13620 | 0.49 | 962 | 22 | 53.7 | -78280 | 0.56 | -81.4 | 78680 | False | False |
| OFF: LTP-EMA20, GapUpRSI | ON: EMAgap, RSI, PCR, SENSEX-PCRmin, ATR | 581 | -44760 | 0.56 | 236 | -16950 | 0.61 | 131 | 4 | -16600 | 0.41 | 948 | 19 | 53.1 | -78310 | 0.55 | -82.6 | 78230 | False | False |
| OFF: LTP-EMA20, GapUpRSI, ATR | ON: EMAgap, RSI, PCR, SENSEX-PCRmin | 609 | -45820 | 0.56 | 239 | -17630 | 0.6 | 132 | 4 | -16120 | 0.43 | 980 | 19 | 53.5 | -79570 | 0.55 | -81.2 | 79490 | False | False |
| OFF: LTP-EMA20, GapUpRSI, SENSEX-PCRmin | ON: EMAgap, RSI, PCR, ATR | 583 | -44920 | 0.56 | 236 | -16950 | 0.61 | 136 | 4 | -18000 | 0.39 | 955 | 19 | 52.9 | -79870 | 0.54 | -83.6 | 79470 | False | False |
| OFF: LTP-EMA20, GapUpRSI, SENSEX-PCRmin, ATR | ON: EMAgap, RSI, PCR | 611 | -45980 | 0.56 | 239 | -17630 | 0.6 | 137 | 4 | -17520 | 0.41 | 987 | 19 | 53.2 | -81130 | 0.55 | -82.2 | 80730 | False | False |
| OFF: LTP-EMA20, EMAgap, GapUpRSI | ON: RSI, PCR, SENSEX-PCRmin, ATR | 603 | -44640 | 0.58 | 247 | -20440 | 0.58 | 131 | 4 | -16600 | 0.41 | 981 | 19 | 53.3 | -81680 | 0.55 | -83.3 | 82400 | False | False |
| OFF: GapUpRSI, PCR | ON: LTP-EMA20, EMAgap, RSI, SENSEX-PCRmin, ATR | 534 | -48410 | 0.52 | 307 | -23250 | 0.57 | 116 | 4 | -10420 | 0.52 | 957 | 19 | 52.9 | -82080 | 0.53 | -85.8 | 82300 | False | False |
| OFF: GapUpRSI, PCR, SENSEX-PCRmin | ON: LTP-EMA20, EMAgap, RSI, ATR | 535 | -48290 | 0.52 | 307 | -23250 | 0.57 | 117 | 4 | -10700 | 0.52 | 959 | 19 | 52.9 | -82240 | 0.53 | -85.8 | 82180 | False | False |
| OFF: GapUpRSI, PCR, ATR | ON: LTP-EMA20, EMAgap, RSI, SENSEX-PCRmin | 565 | -48970 | 0.52 | 313 | -24890 | 0.56 | 121 | 4 | -9140 | 0.58 | 999 | 20 | 53.3 | -83000 | 0.54 | -83.1 | 83220 | False | False |
| OFF: GapUpRSI, PCR, SENSEX-PCRmin, ATR | ON: LTP-EMA20, EMAgap, RSI | 566 | -48850 | 0.53 | 313 | -24890 | 0.56 | 122 | 4 | -9420 | 0.57 | 1001 | 20 | 53.3 | -83160 | 0.54 | -83.1 | 83100 | False | False |
| OFF: LTP-EMA20, EMAgap, GapUpRSI, SENSEX-PCRmin | ON: RSI, PCR, ATR | 605 | -44800 | 0.58 | 247 | -20440 | 0.58 | 136 | 4 | -18000 | 0.39 | 988 | 19 | 53.0 | -83240 | 0.55 | -84.3 | 83640 | False | False |
| OFF: LTP-EMA20, EMAgap, GapUpRSI, ATR | ON: RSI, PCR, SENSEX-PCRmin | 632 | -46100 | 0.58 | 250 | -21120 | 0.57 | 132 | 4 | -16120 | 0.43 | 1014 | 19 | 53.6 | -83340 | 0.55 | -82.2 | 84060 | False | False |
| OFF: EMAgap | ON: LTP-EMA20, RSI, GapUpRSI, PCR, SENSEX-PCRmin, ATR | 620 | -48490 | 0.57 | 170 | -16200 | 0.54 | 172 | 4 | -19320 | 0.44 | 962 | 21 | 52.5 | -84010 | 0.54 | -87.3 | 84610 | False | False |
| OFF: EMAgap, GapUpRSI, PCR | ON: LTP-EMA20, RSI, SENSEX-PCRmin, ATR | 553 | -49810 | 0.52 | 315 | -23820 | 0.58 | 116 | 4 | -10420 | 0.52 | 984 | 19 | 53.1 | -84050 | 0.54 | -85.4 | 85070 | False | False |
| OFF: EMAgap, SENSEX-PCRmin | ON: LTP-EMA20, RSI, GapUpRSI, PCR, ATR | 621 | -48370 | 0.57 | 170 | -16200 | 0.54 | 173 | 4 | -19600 | 0.44 | 964 | 21 | 52.5 | -84170 | 0.54 | -87.3 | 84770 | False | False |
| OFF: EMAgap, GapUpRSI, PCR, SENSEX-PCRmin | ON: LTP-EMA20, RSI, ATR | 554 | -49690 | 0.52 | 315 | -23820 | 0.58 | 117 | 4 | -10700 | 0.52 | 986 | 19 | 53.1 | -84210 | 0.54 | -85.4 | 84950 | False | False |
| OFF: LTP-EMA20, EMAgap, GapUpRSI, SENSEX-PCRmin, ATR | ON: RSI, PCR | 634 | -46260 | 0.58 | 250 | -21120 | 0.57 | 137 | 4 | -17520 | 0.41 | 1021 | 19 | 53.3 | -84900 | 0.55 | -83.2 | 85300 | False | False |
| OFF: EMAgap, GapUpRSI, PCR, ATR | ON: LTP-EMA20, RSI, SENSEX-PCRmin | 584 | -50370 | 0.53 | 321 | -25460 | 0.56 | 121 | 4 | -9140 | 0.58 | 1026 | 20 | 53.6 | -84970 | 0.55 | -82.8 | 85990 | False | False |
| OFF: EMAgap, GapUpRSI, PCR, SENSEX-PCRmin, ATR | ON: LTP-EMA20, RSI | 585 | -50250 | 0.53 | 321 | -25460 | 0.56 | 122 | 4 | -9420 | 0.57 | 1028 | 20 | 53.6 | -85130 | 0.55 | -82.8 | 85870 | False | False |
| OFF: LTP-EMA20 | ON: EMAgap, RSI, GapUpRSI, PCR, SENSEX-PCRmin, ATR | 647 | -44840 | 0.6 | 197 | -18110 | 0.54 | 160 | 5 | -22600 | 0.36 | 1004 | 22 | 52.8 | -85550 | 0.54 | -85.2 | 86350 | False | False |
| OFF: EMAgap, ATR | ON: LTP-EMA20, RSI, GapUpRSI, PCR, SENSEX-PCRmin | 655 | -50870 | 0.57 | 173 | -16880 | 0.53 | 180 | 4 | -19200 | 0.46 | 1008 | 21 | 52.7 | -86950 | 0.54 | -86.3 | 87550 | False | False |
| OFF: LTP-EMA20, SENSEX-PCRmin | ON: EMAgap, RSI, GapUpRSI, PCR, ATR | 649 | -45000 | 0.59 | 197 | -18110 | 0.54 | 168 | 5 | -24040 | 0.35 | 1014 | 22 | 52.6 | -87150 | 0.53 | -85.9 | 87950 | False | False |
| OFF: LTP-EMA20, ATR | ON: EMAgap, RSI, GapUpRSI, PCR, SENSEX-PCRmin | 679 | -46220 | 0.6 | 200 | -18790 | 0.53 | 167 | 5 | -22200 | 0.38 | 1046 | 22 | 53.2 | -87210 | 0.54 | -83.4 | 88010 | False | False |
| OFF: EMAgap, SENSEX-PCRmin, ATR | ON: LTP-EMA20, RSI, GapUpRSI, PCR | 659 | -51190 | 0.57 | 173 | -16880 | 0.53 | 181 | 4 | -19480 | 0.45 | 1013 | 21 | 52.6 | -87550 | 0.54 | -86.4 | 88150 | False | False |
| OFF: EMAgap, PCR | ON: LTP-EMA20, RSI, GapUpRSI, SENSEX-PCRmin, ATR | 638 | -49170 | 0.58 | 250 | -19060 | 0.61 | 175 | 4 | -19400 | 0.45 | 1063 | 21 | 53.3 | -87630 | 0.56 | -82.4 | 88230 | False | False |
| OFF: EMAgap, PCR, SENSEX-PCRmin | ON: LTP-EMA20, RSI, GapUpRSI, ATR | 639 | -49050 | 0.58 | 250 | -19060 | 0.61 | 176 | 4 | -19680 | 0.44 | 1065 | 21 | 53.3 | -87790 | 0.56 | -82.4 | 88390 | False | False |
| OFF: LTP-EMA20, GapUpRSI, PCR | ON: EMAgap, RSI, SENSEX-PCRmin, ATR | 628 | -49160 | 0.55 | 376 | -23020 | 0.64 | 134 | 4 | -16680 | 0.42 | 1138 | 19 | 53.9 | -88860 | 0.56 | -78.1 | 88780 | False | False |
| OFF: LTP-EMA20, SENSEX-PCRmin, ATR | ON: EMAgap, RSI, GapUpRSI, PCR | 683 | -46540 | 0.6 | 200 | -18790 | 0.53 | 175 | 5 | -23640 | 0.37 | 1058 | 22 | 53.0 | -88970 | 0.54 | -84.1 | 89770 | False | False |
| OFF: LTP-EMA20, PCR | ON: EMAgap, RSI, GapUpRSI, SENSEX-PCRmin, ATR | 666 | -45680 | 0.6 | 287 | -20620 | 0.61 | 163 | 5 | -22680 | 0.36 | 1116 | 22 | 53.8 | -88980 | 0.56 | -79.7 | 89780 | False | False |
| OFF: LTP-EMA20, GapUpRSI, PCR, ATR | ON: EMAgap, RSI, SENSEX-PCRmin | 661 | -50020 | 0.56 | 382 | -24660 | 0.62 | 139 | 4 | -15400 | 0.46 | 1182 | 20 | 54.3 | -90080 | 0.57 | -76.2 | 90000 | False | False |
| OFF: LTP-EMA20, GapUpRSI, PCR, SENSEX-PCRmin | ON: EMAgap, RSI, ATR | 630 | -49320 | 0.55 | 376 | -23020 | 0.64 | 139 | 4 | -18080 | 0.4 | 1145 | 19 | 53.7 | -90420 | 0.56 | -79.0 | 90020 | False | False |
| OFF: LTP-EMA20, EMAgap, GapUpRSI, PCR | ON: RSI, SENSEX-PCRmin, ATR | 651 | -48920 | 0.57 | 391 | -24870 | 0.64 | 134 | 4 | -16680 | 0.42 | 1176 | 19 | 54.2 | -90470 | 0.57 | -76.9 | 91190 | False | False |
| OFF: LTP-EMA20, PCR, ATR | ON: EMAgap, RSI, GapUpRSI, SENSEX-PCRmin | 704 | -46740 | 0.6 | 293 | -22260 | 0.6 | 174 | 5 | -21480 | 0.4 | 1171 | 23 | 54.3 | -90480 | 0.57 | -77.3 | 91280 | False | False |
| OFF: LTP-EMA20, PCR, SENSEX-PCRmin | ON: EMAgap, RSI, GapUpRSI, ATR | 668 | -45840 | 0.6 | 287 | -20620 | 0.61 | 171 | 5 | -24120 | 0.35 | 1126 | 22 | 53.5 | -90580 | 0.56 | -80.4 | 91380 | False | False |
| OFF: EMAgap, PCR, ATR | ON: LTP-EMA20, RSI, GapUpRSI, SENSEX-PCRmin | 684 | -52230 | 0.57 | 256 | -20700 | 0.59 | 187 | 4 | -18480 | 0.48 | 1127 | 22 | 53.5 | -91410 | 0.56 | -81.1 | 92010 | False | False |
| OFF: LTP-EMA20, GapUpRSI, PCR, SENSEX-PCRmin, ATR | ON: EMAgap, RSI | 663 | -50180 | 0.56 | 382 | -24660 | 0.62 | 144 | 4 | -16800 | 0.44 | 1189 | 20 | 54.1 | -91640 | 0.56 | -77.1 | 91240 | False | False |
| OFF: EMAgap, PCR, SENSEX-PCRmin, ATR | ON: LTP-EMA20, RSI, GapUpRSI | 688 | -52550 | 0.57 | 256 | -20700 | 0.59 | 188 | 4 | -18760 | 0.48 | 1132 | 22 | 53.4 | -92010 | 0.56 | -81.3 | 92610 | False | False |
| OFF: LTP-EMA20, EMAgap, GapUpRSI, PCR, SENSEX-PCRmin | ON: RSI, ATR | 653 | -49080 | 0.57 | 391 | -24870 | 0.64 | 139 | 4 | -18080 | 0.4 | 1183 | 19 | 54.0 | -92030 | 0.57 | -77.8 | 92430 | False | False |
| OFF: LTP-EMA20, EMAgap, GapUpRSI, PCR, ATR | ON: RSI, SENSEX-PCRmin | 685 | -50180 | 0.57 | 397 | -26510 | 0.63 | 139 | 4 | -15400 | 0.46 | 1221 | 20 | 54.6 | -92090 | 0.58 | -75.4 | 92810 | False | False |
| OFF: LTP-EMA20, PCR, SENSEX-PCRmin, ATR | ON: EMAgap, RSI, GapUpRSI | 708 | -47060 | 0.6 | 293 | -22260 | 0.6 | 182 | 5 | -22920 | 0.39 | 1183 | 23 | 54.1 | -92240 | 0.56 | -78.0 | 93040 | False | False |
| OFF: LTP-EMA20, EMAgap, GapUpRSI, PCR, SENSEX-PCRmin, ATR | ON: RSI | 687 | -50340 | 0.57 | 397 | -26510 | 0.63 | 144 | 4 | -16800 | 0.44 | 1228 | 20 | 54.3 | -93650 | 0.57 | -76.3 | 94050 | False | False |
| OFF: LTP-EMA20, EMAgap | ON: RSI, GapUpRSI, PCR, SENSEX-PCRmin, ATR | 754 | -52600 | 0.6 | 245 | -18830 | 0.6 | 217 | 5 | -28620 | 0.38 | 1216 | 22 | 53.1 | -100050 | 0.55 | -82.3 | 101050 | False | False |
| OFF: LTP-EMA20, EMAgap, SENSEX-PCRmin | ON: RSI, GapUpRSI, PCR, ATR | 756 | -52760 | 0.6 | 245 | -18830 | 0.6 | 225 | 5 | -30060 | 0.37 | 1226 | 22 | 52.9 | -101650 | 0.55 | -82.9 | 102650 | False | False |
| OFF: LTP-EMA20, EMAgap, PCR | ON: RSI, GapUpRSI, SENSEX-PCRmin, ATR | 777 | -53580 | 0.6 | 338 | -20180 | 0.67 | 220 | 5 | -28700 | 0.38 | 1335 | 22 | 54.0 | -102460 | 0.58 | -76.7 | 103460 | False | False |
| OFF: LTP-EMA20, EMAgap, ATR | ON: RSI, GapUpRSI, PCR, SENSEX-PCRmin | 796 | -56000 | 0.59 | 248 | -19510 | 0.59 | 225 | 5 | -28500 | 0.39 | 1269 | 22 | 53.3 | -104010 | 0.55 | -82.0 | 104610 | False | False |
| OFF: LTP-EMA20, EMAgap, PCR, SENSEX-PCRmin | ON: RSI, GapUpRSI, ATR | 779 | -53740 | 0.6 | 338 | -20180 | 0.67 | 228 | 5 | -30140 | 0.37 | 1345 | 22 | 53.8 | -104060 | 0.57 | -77.4 | 105060 | False | False |
| OFF: LTP-EMA20, EMAgap, SENSEX-PCRmin, ATR | ON: RSI, GapUpRSI, PCR | 803 | -56760 | 0.59 | 248 | -19510 | 0.59 | 236 | 5 | -30380 | 0.38 | 1287 | 22 | 53.0 | -106650 | 0.55 | -82.9 | 107250 | False | False |
| OFF: LTP-EMA20, EMAgap, PCR, ATR | ON: RSI, GapUpRSI, SENSEX-PCRmin | 829 | -57490 | 0.59 | 344 | -21820 | 0.65 | 232 | 5 | -27780 | 0.41 | 1405 | 23 | 54.1 | -107090 | 0.57 | -76.2 | 107690 | False | False |
| OFF: LTP-EMA20, EMAgap, PCR, SENSEX-PCRmin, ATR | ON: RSI, GapUpRSI | 836 | -58250 | 0.59 | 344 | -21820 | 0.65 | 243 | 5 | -29660 | 0.4 | 1423 | 23 | 53.9 | -109730 | 0.57 | -77.1 | 110330 | False | False |
| OFF: RSI | ON: LTP-EMA20, EMAgap, GapUpRSI, PCR, SENSEX-PCRmin, ATR | 914 | -82860 | 0.52 | 297 | -39170 | 0.42 | 242 | 5 | -25150 | 0.46 | 1453 | 23 | 50.2 | -147180 | 0.49 | -101.3 | 147380 | False | False |
| OFF: RSI, SENSEX-PCRmin | ON: LTP-EMA20, EMAgap, GapUpRSI, PCR, ATR | 918 | -83580 | 0.52 | 297 | -39170 | 0.42 | 243 | 5 | -25430 | 0.46 | 1458 | 23 | 50.1 | -148180 | 0.49 | -101.6 | 148380 | False | False |
| OFF: RSI, ATR | ON: LTP-EMA20, EMAgap, GapUpRSI, PCR, SENSEX-PCRmin | 973 | -86800 | 0.52 | 310 | -42370 | 0.41 | 254 | 5 | -24550 | 0.49 | 1537 | 23 | 50.5 | -153720 | 0.49 | -100.0 | 153920 | False | False |
| OFF: RSI, SENSEX-PCRmin, ATR | ON: LTP-EMA20, EMAgap, GapUpRSI, PCR | 977 | -87520 | 0.52 | 310 | -42370 | 0.41 | 255 | 5 | -24830 | 0.48 | 1542 | 23 | 50.4 | -154720 | 0.49 | -100.3 | 154920 | False | False |
| OFF: RSI, PCR | ON: LTP-EMA20, EMAgap, GapUpRSI, SENSEX-PCRmin, ATR | 942 | -83660 | 0.53 | 482 | -51500 | 0.48 | 256 | 5 | -26990 | 0.46 | 1680 | 23 | 50.8 | -162150 | 0.5 | -96.5 | 162350 | False | False |
| OFF: RSI, PCR, SENSEX-PCRmin | ON: LTP-EMA20, EMAgap, GapUpRSI, ATR | 946 | -84380 | 0.52 | 482 | -51500 | 0.48 | 257 | 5 | -27270 | 0.45 | 1685 | 23 | 50.7 | -163150 | 0.5 | -96.8 | 163350 | False | False |
| OFF: RSI, GapUpRSI | ON: LTP-EMA20, EMAgap, PCR, SENSEX-PCRmin, ATR | 1046 | -95310 | 0.51 | 421 | -42240 | 0.51 | 286 | 5 | -28090 | 0.48 | 1753 | 23 | 51.1 | -165640 | 0.51 | -94.5 | 166240 | False | False |
| OFF: RSI, GapUpRSI, SENSEX-PCRmin | ON: LTP-EMA20, EMAgap, PCR, ATR | 1050 | -96030 | 0.51 | 421 | -42240 | 0.51 | 288 | 5 | -28650 | 0.48 | 1759 | 23 | 51.0 | -166920 | 0.5 | -94.9 | 167520 | False | False |
| OFF: RSI, PCR, ATR | ON: LTP-EMA20, EMAgap, GapUpRSI, SENSEX-PCRmin | 1014 | -86840 | 0.53 | 501 | -56620 | 0.46 | 271 | 5 | -25790 | 0.49 | 1786 | 23 | 51.2 | -169250 | 0.5 | -94.8 | 169450 | False | False |
| OFF: RSI, PCR, SENSEX-PCRmin, ATR | ON: LTP-EMA20, EMAgap, GapUpRSI | 1018 | -87560 | 0.53 | 501 | -56620 | 0.46 | 272 | 5 | -26070 | 0.49 | 1791 | 23 | 51.1 | -170250 | 0.5 | -95.1 | 170450 | False | False |
| OFF: RSI, GapUpRSI, ATR | ON: LTP-EMA20, EMAgap, PCR, SENSEX-PCRmin | 1111 | -99330 | 0.51 | 434 | -45440 | 0.49 | 299 | 5 | -27770 | 0.5 | 1844 | 23 | 51.4 | -172540 | 0.51 | -93.6 | 173140 | False | False |
| OFF: RSI, GapUpRSI, SENSEX-PCRmin, ATR | ON: LTP-EMA20, EMAgap, PCR | 1115 | -100050 | 0.51 | 434 | -45440 | 0.49 | 301 | 5 | -28330 | 0.49 | 1850 | 23 | 51.3 | -173820 | 0.5 | -94.0 | 174420 | False | False |
| OFF: LTP-EMA20, RSI | ON: EMAgap, GapUpRSI, PCR, SENSEX-PCRmin, ATR | 1164 | -98200 | 0.54 | 448 | -51870 | 0.45 | 325 | 5 | -36990 | 0.43 | 1937 | 23 | 50.8 | -187060 | 0.5 | -96.6 | 187660 | False | False |
| OFF: LTP-EMA20, RSI, SENSEX-PCRmin | ON: EMAgap, GapUpRSI, PCR, ATR | 1172 | -99640 | 0.54 | 448 | -51870 | 0.45 | 335 | 5 | -38590 | 0.42 | 1955 | 23 | 50.6 | -190100 | 0.49 | -97.2 | 190700 | False | False |
| OFF: RSI, GapUpRSI, PCR | ON: LTP-EMA20, EMAgap, SENSEX-PCRmin, ATR | 1112 | -100990 | 0.51 | 689 | -59770 | 0.53 | 300 | 5 | -29930 | 0.48 | 2101 | 23 | 51.6 | -190690 | 0.51 | -90.8 | 191290 | False | False |
| OFF: RSI, GapUpRSI, PCR, SENSEX-PCRmin | ON: LTP-EMA20, EMAgap, ATR | 1116 | -101710 | 0.51 | 689 | -59770 | 0.53 | 302 | 5 | -30490 | 0.47 | 2107 | 23 | 51.5 | -191970 | 0.51 | -91.1 | 192570 | False | False |
| OFF: LTP-EMA20, RSI, ATR | ON: EMAgap, GapUpRSI, PCR, SENSEX-PCRmin | 1235 | -102300 | 0.54 | 465 | -55430 | 0.44 | 337 | 5 | -36390 | 0.44 | 2037 | 23 | 51.1 | -194120 | 0.5 | -95.3 | 194720 | False | False |
| OFF: EMAgap, RSI | ON: LTP-EMA20, GapUpRSI, PCR, SENSEX-PCRmin, ATR | 1196 | -108740 | 0.53 | 395 | -52910 | 0.43 | 330 | 5 | -32860 | 0.49 | 1921 | 23 | 50.2 | -194510 | 0.5 | -101.3 | 195110 | False | False |
| OFF: EMAgap, RSI, SENSEX-PCRmin | ON: LTP-EMA20, GapUpRSI, PCR, ATR | 1201 | -109340 | 0.53 | 395 | -52910 | 0.43 | 331 | 5 | -33140 | 0.49 | 1927 | 23 | 50.2 | -195390 | 0.5 | -101.4 | 195990 | False | False |
| OFF: LTP-EMA20, RSI, SENSEX-PCRmin, ATR | ON: EMAgap, GapUpRSI, PCR | 1245 | -103900 | 0.54 | 465 | -55430 | 0.44 | 347 | 5 | -37990 | 0.44 | 2057 | 23 | 50.9 | -197320 | 0.49 | -95.9 | 197920 | False | False |
| OFF: RSI, GapUpRSI, PCR, ATR | ON: LTP-EMA20, EMAgap, SENSEX-PCRmin | 1190 | -104250 | 0.52 | 708 | -64890 | 0.51 | 316 | 5 | -29010 | 0.5 | 2214 | 23 | 51.9 | -198150 | 0.51 | -89.5 | 198750 | False | False |
| OFF: RSI, GapUpRSI, PCR, SENSEX-PCRmin, ATR | ON: LTP-EMA20, EMAgap | 1194 | -104970 | 0.52 | 708 | -64890 | 0.51 | 318 | 5 | -29570 | 0.5 | 2220 | 23 | 51.8 | -199430 | 0.51 | -89.8 | 200030 | False | False |
| OFF: LTP-EMA20, RSI, PCR | ON: EMAgap, GapUpRSI, SENSEX-PCRmin, ATR | 1195 | -98960 | 0.54 | 660 | -61860 | 0.51 | 339 | 5 | -38830 | 0.42 | 2194 | 23 | 51.5 | -199650 | 0.51 | -91.0 | 200250 | False | False |
| OFF: LTP-EMA20, RSI, PCR, SENSEX-PCRmin | ON: EMAgap, GapUpRSI, ATR | 1203 | -100400 | 0.54 | 660 | -61860 | 0.51 | 349 | 5 | -40430 | 0.42 | 2212 | 23 | 51.3 | -202690 | 0.51 | -91.6 | 203290 | False | False |
| OFF: LTP-EMA20, RSI, GapUpRSI | ON: EMAgap, PCR, SENSEX-PCRmin, ATR | 1314 | -110930 | 0.53 | 598 | -54580 | 0.53 | 373 | 5 | -39240 | 0.45 | 2285 | 23 | 51.8 | -204750 | 0.52 | -89.6 | 205550 | False | False |
| OFF: EMAgap, RSI, ATR | ON: LTP-EMA20, GapUpRSI, PCR, SENSEX-PCRmin | 1282 | -116620 | 0.52 | 411 | -57490 | 0.41 | 346 | 5 | -31930 | 0.51 | 2039 | 23 | 50.2 | -206040 | 0.49 | -101.0 | 206640 | False | False |
| OFF: EMAgap, RSI, PCR | ON: LTP-EMA20, GapUpRSI, SENSEX-PCRmin, ATR | 1228 | -109900 | 0.53 | 599 | -62330 | 0.5 | 344 | 5 | -34700 | 0.49 | 2171 | 23 | 51.0 | -206930 | 0.52 | -95.3 | 207530 | False | False |
| OFF: LTP-EMA20, RSI, PCR, ATR | ON: EMAgap, GapUpRSI, SENSEX-PCRmin | 1279 | -102300 | 0.55 | 683 | -67340 | 0.5 | 354 | 5 | -37630 | 0.45 | 2316 | 23 | 51.8 | -207270 | 0.52 | -89.5 | 207870 | False | False |
| OFF: EMAgap, RSI, SENSEX-PCRmin, ATR | ON: LTP-EMA20, GapUpRSI, PCR | 1290 | -117660 | 0.52 | 411 | -57490 | 0.41 | 347 | 5 | -32210 | 0.51 | 2048 | 23 | 50.2 | -207360 | 0.49 | -101.2 | 207960 | False | False |
| OFF: EMAgap, RSI, PCR, SENSEX-PCRmin | ON: LTP-EMA20, GapUpRSI, ATR | 1233 | -110500 | 0.53 | 599 | -62330 | 0.5 | 345 | 5 | -34980 | 0.48 | 2177 | 23 | 50.9 | -207810 | 0.52 | -95.5 | 208410 | False | False |
| OFF: LTP-EMA20, RSI, GapUpRSI, SENSEX-PCRmin | ON: EMAgap, PCR, ATR | 1322 | -112370 | 0.53 | 598 | -54580 | 0.53 | 386 | 5 | -41680 | 0.44 | 2306 | 23 | 51.6 | -208630 | 0.51 | -90.5 | 209430 | False | False |
| OFF: LTP-EMA20, RSI, PCR, SENSEX-PCRmin, ATR | ON: EMAgap, GapUpRSI | 1289 | -103900 | 0.55 | 683 | -67340 | 0.5 | 364 | 5 | -39230 | 0.44 | 2336 | 23 | 51.6 | -210470 | 0.51 | -90.1 | 211070 | False | False |
| OFF: LTP-EMA20, RSI, GapUpRSI, ATR | ON: EMAgap, PCR, SENSEX-PCRmin | 1394 | -115150 | 0.53 | 615 | -58140 | 0.51 | 386 | 5 | -38920 | 0.47 | 2395 | 23 | 52.1 | -212210 | 0.52 | -88.6 | 213010 | False | False |
| OFF: LTP-EMA20, RSI, GapUpRSI, SENSEX-PCRmin, ATR | ON: EMAgap, PCR | 1404 | -116750 | 0.53 | 615 | -58140 | 0.51 | 399 | 5 | -41360 | 0.45 | 2418 | 23 | 51.8 | -216250 | 0.51 | -89.4 | 217050 | False | False |
| OFF: EMAgap, RSI, GapUpRSI | ON: LTP-EMA20, PCR, SENSEX-PCRmin, ATR | 1335 | -120490 | 0.53 | 519 | -55980 | 0.49 | 387 | 5 | -40420 | 0.48 | 2241 | 23 | 50.9 | -216890 | 0.51 | -96.8 | 217890 | False | False |
| OFF: EMAgap, RSI, GapUpRSI, SENSEX-PCRmin | ON: LTP-EMA20, PCR, ATR | 1340 | -121090 | 0.53 | 519 | -55980 | 0.49 | 389 | 5 | -40980 | 0.47 | 2248 | 23 | 50.8 | -218050 | 0.51 | -97.0 | 219050 | False | False |
| OFF: EMAgap, RSI, PCR, ATR | ON: LTP-EMA20, GapUpRSI, SENSEX-PCRmin | 1332 | -118020 | 0.53 | 621 | -68830 | 0.48 | 364 | 5 | -33050 | 0.52 | 2317 | 23 | 51.0 | -219900 | 0.51 | -94.9 | 220500 | False | False |
| OFF: EMAgap, RSI, PCR, SENSEX-PCRmin, ATR | ON: LTP-EMA20, GapUpRSI | 1340 | -119060 | 0.53 | 621 | -68830 | 0.48 | 365 | 5 | -33330 | 0.51 | 2326 | 23 | 50.9 | -221220 | 0.51 | -95.1 | 221820 | False | False |
| OFF: LTP-EMA20, RSI, GapUpRSI, PCR | ON: EMAgap, SENSEX-PCRmin, ATR | 1385 | -116770 | 0.53 | 901 | -70010 | 0.56 | 387 | 5 | -41080 | 0.45 | 2673 | 23 | 52.3 | -227860 | 0.53 | -85.2 | 228660 | False | False |
| OFF: EMAgap, RSI, GapUpRSI, ATR | ON: LTP-EMA20, PCR, SENSEX-PCRmin | 1429 | -129550 | 0.52 | 535 | -60560 | 0.47 | 406 | 5 | -39930 | 0.49 | 2370 | 23 | 50.8 | -230040 | 0.5 | -97.1 | 231040 | False | False |
| OFF: EMAgap, RSI, GapUpRSI, SENSEX-PCRmin, ATR | ON: LTP-EMA20, PCR | 1437 | -130590 | 0.52 | 535 | -60560 | 0.47 | 408 | 5 | -40490 | 0.49 | 2380 | 23 | 50.7 | -231640 | 0.5 | -97.3 | 232640 | False | False |
| OFF: LTP-EMA20, RSI, GapUpRSI, PCR, SENSEX-PCRmin | ON: EMAgap, ATR | 1393 | -118210 | 0.53 | 901 | -70010 | 0.56 | 400 | 5 | -43520 | 0.44 | 2694 | 23 | 52.1 | -231740 | 0.53 | -86.0 | 232540 | False | False |
| OFF: LTP-EMA20, RSI, GapUpRSI, PCR, ATR | ON: EMAgap, SENSEX-PCRmin | 1478 | -120230 | 0.54 | 924 | -75490 | 0.55 | 403 | 5 | -40160 | 0.47 | 2805 | 23 | 52.6 | -235880 | 0.53 | -84.1 | 236680 | False | False |
| OFF: EMAgap, RSI, GapUpRSI, PCR | ON: LTP-EMA20, SENSEX-PCRmin, ATR | 1406 | -126410 | 0.53 | 807 | -70120 | 0.54 | 401 | 5 | -42260 | 0.47 | 2614 | 23 | 51.5 | -238790 | 0.52 | -91.4 | 240090 | False | False |
| OFF: LTP-EMA20, RSI, GapUpRSI, PCR, SENSEX-PCRmin, ATR | ON: EMAgap | 1488 | -121830 | 0.54 | 924 | -75490 | 0.55 | 416 | 5 | -42600 | 0.46 | 2828 | 23 | 52.4 | -239920 | 0.53 | -84.8 | 240720 | False | False |
| OFF: EMAgap, RSI, GapUpRSI, PCR, SENSEX-PCRmin | ON: LTP-EMA20, ATR | 1411 | -127010 | 0.53 | 807 | -70120 | 0.54 | 403 | 5 | -42820 | 0.47 | 2621 | 23 | 51.4 | -239950 | 0.52 | -91.5 | 241250 | False | False |
| OFF: EMAgap, RSI, GapUpRSI, PCR, ATR | ON: LTP-EMA20, SENSEX-PCRmin | 1518 | -135710 | 0.52 | 829 | -76620 | 0.52 | 424 | 5 | -41050 | 0.49 | 2771 | 23 | 51.5 | -253380 | 0.52 | -91.4 | 254680 | False | False |
| OFF: EMAgap, RSI, GapUpRSI, PCR, SENSEX-PCRmin, ATR | ON: LTP-EMA20 | 1526 | -136750 | 0.52 | 829 | -76620 | 0.52 | 426 | 5 | -41610 | 0.49 | 2781 | 23 | 51.4 | -254980 | 0.52 | -91.7 | 256280 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI | ON: GapUpRSI, PCR, SENSEX-PCRmin, ATR | 1567 | -137180 | 0.54 | 637 | -78390 | 0.44 | 457 | 5 | -48820 | 0.46 | 2661 | 23 | 50.5 | -264390 | 0.5 | -99.4 | 265190 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, SENSEX-PCRmin | ON: GapUpRSI, PCR, ATR | 1582 | -139380 | 0.53 | 638 | -78670 | 0.44 | 468 | 5 | -50700 | 0.46 | 2688 | 23 | 50.3 | -268750 | 0.5 | -100.0 | 269550 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, PCR | ON: GapUpRSI, SENSEX-PCRmin, ATR | 1604 | -138640 | 0.54 | 872 | -86100 | 0.51 | 471 | 5 | -50660 | 0.46 | 2947 | 23 | 51.2 | -275400 | 0.52 | -93.5 | 276200 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, ATR | ON: GapUpRSI, PCR, SENSEX-PCRmin | 1678 | -146260 | 0.53 | 659 | -84430 | 0.43 | 474 | 5 | -48290 | 0.48 | 2811 | 23 | 50.4 | -278980 | 0.5 | -99.2 | 279580 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, PCR, SENSEX-PCRmin | ON: GapUpRSI, ATR | 1619 | -140840 | 0.54 | 873 | -86380 | 0.51 | 482 | 5 | -52540 | 0.45 | 2974 | 23 | 51.0 | -279760 | 0.52 | -94.1 | 280560 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, SENSEX-PCRmin, ATR | ON: GapUpRSI, PCR | 1699 | -149340 | 0.53 | 660 | -84710 | 0.43 | 488 | 5 | -50610 | 0.47 | 2847 | 23 | 50.2 | -284660 | 0.49 | -100.0 | 285260 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, GapUpRSI | ON: PCR, SENSEX-PCRmin, ATR | 1729 | -149220 | 0.54 | 790 | -80310 | 0.51 | 524 | 5 | -56380 | 0.46 | 3043 | 23 | 51.3 | -285910 | 0.51 | -94.0 | 287310 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, GapUpRSI, SENSEX-PCRmin | ON: PCR, ATR | 1744 | -151420 | 0.53 | 791 | -80590 | 0.5 | 538 | 5 | -59100 | 0.45 | 3073 | 23 | 51.0 | -291110 | 0.51 | -94.7 | 292510 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, PCR, ATR | ON: GapUpRSI, SENSEX-PCRmin | 1733 | -148070 | 0.54 | 900 | -94060 | 0.5 | 492 | 5 | -49410 | 0.48 | 3125 | 23 | 51.1 | -291540 | 0.52 | -93.3 | 292140 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, PCR, SENSEX-PCRmin, ATR | ON: GapUpRSI | 1754 | -151150 | 0.53 | 901 | -94340 | 0.49 | 506 | 5 | -51730 | 0.47 | 3161 | 23 | 50.9 | -297220 | 0.51 | -94.0 | 297820 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, GapUpRSI, ATR | ON: PCR, SENSEX-PCRmin | 1851 | -159520 | 0.53 | 812 | -86350 | 0.49 | 548 | 5 | -56210 | 0.47 | 3211 | 23 | 51.2 | -302080 | 0.51 | -94.1 | 303480 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, GapUpRSI, PCR | ON: SENSEX-PCRmin, ATR | 1807 | -155640 | 0.54 | 1117 | -92980 | 0.56 | 538 | 5 | -58220 | 0.46 | 3462 | 23 | 51.9 | -306840 | 0.53 | -88.6 | 308240 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, GapUpRSI, SENSEX-PCRmin, ATR | ON: PCR | 1872 | -162600 | 0.53 | 813 | -86630 | 0.49 | 565 | 5 | -59370 | 0.46 | 3250 | 23 | 50.9 | -308600 | 0.51 | -95.0 | 310000 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, GapUpRSI, PCR, SENSEX-PCRmin | ON: ATR | 1822 | -157840 | 0.53 | 1118 | -93260 | 0.56 | 552 | 5 | -60940 | 0.45 | 3492 | 23 | 51.6 | -312040 | 0.53 | -89.4 | 313440 | False | False |
| OFF: LTP-EMA20, EMAgap, RSI, GapUpRSI, PCR, ATR | ON: SENSEX-PCRmin | 1947 | -166290 | 0.53 | 1145 | -100940 | 0.54 | 566 | 5 | -57330 | 0.47 | 3658 | 23 | 51.8 | -324560 | 0.53 | -88.7 | 325960 | False | False |

## RSI band (normal open)

| Cell | train_N | train_Net | train_PF | val_N | val_Net | val_PF | hold_N | hold_Days | hold_Net | hold_PF | all_N | all_Days | all_TgtPct | all_Net | all_PF | all_Exp | all_MaxDD | BeatsLive_all3 | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| RSI5m 35-38 | 277 | -17490 | 0.62 | 89 | -8010 | 0.54 | 74 | 4 | -7090 | 0.49 | 440 | 21 | 55.3 | -32590 | 0.58 | -74.1 | 33190 | True | False |
| RSI5m 32-35 | 289 | -20700 | 0.6 | 84 | -6180 | 0.62 | 79 | 4 | -7550 | 0.48 | 452 | 21 | 53.7 | -34430 | 0.58 | -76.2 | 34630 | True | False |
| RSI5m 30-35 | 353 | -23550 | 0.61 | 100 | -10420 | 0.54 | 100 | 4 | -10510 | 0.44 | 553 | 21 | 53.0 | -44480 | 0.56 | -80.4 | 44680 | True | False |
| RSI5m 35-40 | 348 | -25670 | 0.58 | 105 | -11690 | 0.47 | 93 | 4 | -7770 | 0.55 | 546 | 22 | 53.6 | -45130 | 0.55 | -82.7 | 45730 | True | False |
| RSI5m 32-38 | 403 | -29010 | 0.59 | 123 | -11050 | 0.55 | 97 | 4 | -9300 | 0.49 | 623 | 21 | 53.7 | -49360 | 0.56 | -79.2 | 49760 | True | False |
| RSI5m 28-35 | 429 | -34450 | 0.57 | 108 | -11820 | 0.52 | 107 | 4 | -12370 | 0.41 | 644 | 21 | 51.9 | -58640 | 0.53 | -91.1 | 58840 | True | False |
| RSI5m 30-38 | 467 | -32510 | 0.6 | 138 | -14470 | 0.52 | 120 | 4 | -12600 | 0.46 | 725 | 21 | 53.2 | -59580 | 0.56 | -82.2 | 59980 | True | False |
| RSI5m 32-40 | 472 | -36750 | 0.56 | 141 | -14700 | 0.5 | 116 | 4 | -11000 | 0.51 | 729 | 22 | 52.6 | -62450 | 0.54 | -85.7 | 62850 | True | False |
| RSI5m 35-42 | 425 | -35970 | 0.54 | 130 | -15770 | 0.43 | 118 | 5 | -12950 | 0.47 | 673 | 23 | 51.4 | -64690 | 0.5 | -96.1 | 65490 | False | False |
| RSI5m 25-35 | 491 | -42700 | 0.54 | 116 | -13920 | 0.5 | 110 | 4 | -12850 | 0.4 | 717 | 21 | 50.8 | -69470 | 0.51 | -96.9 | 69670 | False | False |
| RSI5m 30-40 | 536 | -40250 | 0.57 | 157 | -18940 | 0.47 | 139 | 4 | -14300 | 0.48 | 832 | 22 | 52.2 | -73490 | 0.53 | -88.3 | 73890 | False | False |
| RSI5m 20-35 | 516 | -47380 | 0.53 | 116 | -13920 | 0.5 | 114 | 4 | -13690 | 0.39 | 746 | 21 | 50.4 | -74990 | 0.5 | -100.5 | 75190 | False | False |
| RSI5m 0-35 | 531 | -48920 | 0.53 | 116 | -13920 | 0.5 | 114 | 4 | -13690 | 0.39 | 761 | 21 | 50.5 | -76530 | 0.5 | -100.6 | 76730 | False | False |
| RSI5m 35-45 | 500 | -43000 | 0.53 | 181 | -23440 | 0.41 | 139 | 5 | -13890 | 0.5 | 820 | 23 | 51.1 | -80330 | 0.5 | -98.0 | 79930 | False | False |
| RSI5m 32-42 | 548 | -46650 | 0.54 | 166 | -18780 | 0.47 | 140 | 5 | -15780 | 0.46 | 854 | 23 | 51.2 | -81210 | 0.51 | -95.1 | 82010 | False | False |
| RSI5m 25-38 | 601 | -49970 | 0.55 | 153 | -17570 | 0.5 | 130 | 4 | -14940 | 0.43 | 884 | 21 | 51.5 | -82480 | 0.52 | -93.3 | 82880 | False | False |
| RSI5m 28-40 | 609 | -50280 | 0.55 | 164 | -19940 | 0.46 | 146 | 4 | -16160 | 0.45 | 919 | 22 | 51.6 | -86380 | 0.52 | -94.0 | 86780 | False | False |
| RSI5m 20-38 | 626 | -54000 | 0.54 | 153 | -17570 | 0.5 | 134 | 4 | -15780 | 0.42 | 913 | 21 | 51.2 | -87350 | 0.51 | -95.7 | 87750 | False | False |
| RSI5m 35-50 | 537 | -44010 | 0.54 | 224 | -28200 | 0.42 | 169 | 5 | -15320 | 0.52 | 930 | 23 | 51.5 | -87530 | 0.5 | -94.1 | 87730 | False | False |
| RSI5m 0-38 | 641 | -55540 | 0.54 | 153 | -17570 | 0.5 | 134 | 4 | -15780 | 0.42 | 928 | 21 | 51.3 | -88890 | 0.51 | -95.8 | 89290 | False | False |
| RSI5m 30-42 | 611 | -50270 | 0.55 | 182 | -23020 | 0.44 | 163 | 5 | -19080 | 0.44 | 956 | 23 | 50.9 | -92370 | 0.51 | -96.6 | 93170 | False | False |
| RSI5m 32-45 | 622 | -53280 | 0.54 | 216 | -25630 | 0.45 | 161 | 5 | -16720 | 0.49 | 999 | 23 | 51.0 | -95630 | 0.51 | -95.7 | 95230 | False | False |
| RSI5m 25-40 | 670 | -57710 | 0.54 | 172 | -22040 | 0.46 | 149 | 4 | -16640 | 0.45 | 991 | 22 | 50.9 | -96390 | 0.51 | -97.3 | 96790 | False | False |
| RSI5m 20-40 | 695 | -61740 | 0.53 | 172 | -22040 | 0.46 | 153 | 4 | -17480 | 0.44 | 1020 | 22 | 50.7 | -101260 | 0.5 | -99.3 | 101660 | False | False |
| RSI5m 32-50 | 658 | -53890 | 0.55 | 259 | -30390 | 0.45 | 191 | 5 | -18150 | 0.5 | 1108 | 23 | 51.4 | -102430 | 0.51 | -92.4 | 102630 | False | False |
| RSI5m 0-40 | 710 | -63280 | 0.53 | 172 | -22040 | 0.46 | 153 | 4 | -17480 | 0.44 | 1035 | 22 | 50.7 | -102800 | 0.5 | -99.3 | 103200 | False | False |
| RSI5m 28-42 | 684 | -60300 | 0.53 | 189 | -24020 | 0.44 | 170 | 5 | -20940 | 0.42 | 1043 | 23 | 50.4 | -105260 | 0.49 | -100.9 | 106060 | False | False |
| RSI5m 30-45 | 685 | -56900 | 0.55 | 232 | -29870 | 0.43 | 184 | 5 | -20020 | 0.47 | 1101 | 23 | 50.8 | -106790 | 0.5 | -97.0 | 106390 | False | False |
| RSI5m 30-50 | 719 | -57310 | 0.56 | 275 | -34630 | 0.43 | 214 | 5 | -21450 | 0.48 | 1208 | 23 | 51.2 | -113390 | 0.51 | -93.9 | 113590 | False | False |
| RSI5m 25-42 | 745 | -67730 | 0.52 | 197 | -26120 | 0.44 | 173 | 5 | -21420 | 0.42 | 1115 | 23 | 49.9 | -115270 | 0.49 | -103.4 | 116070 | False | False |
| RSI5m 28-45 | 758 | -66930 | 0.53 | 239 | -30870 | 0.43 | 191 | 5 | -21880 | 0.45 | 1188 | 23 | 50.4 | -119680 | 0.49 | -100.7 | 119280 | False | False |
| RSI5m 20-42 | 770 | -71760 | 0.52 | 197 | -26120 | 0.44 | 177 | 5 | -22260 | 0.41 | 1144 | 23 | 49.7 | -120140 | 0.48 | -105.0 | 120940 | False | False |
| RSI5m 0-42 | 785 | -73300 | 0.52 | 197 | -26120 | 0.44 | 177 | 5 | -22260 | 0.41 | 1159 | 23 | 49.8 | -121680 | 0.48 | -105.0 | 122480 | False | False |
| RSI5m 28-50 | 792 | -67340 | 0.54 | 282 | -35630 | 0.43 | 221 | 5 | -23310 | 0.47 | 1295 | 23 | 50.8 | -126280 | 0.5 | -97.5 | 126480 | False | False |
| RSI5m 25-45 | 819 | -74360 | 0.52 | 247 | -32970 | 0.43 | 194 | 5 | -22360 | 0.45 | 1260 | 23 | 50.0 | -129690 | 0.49 | -102.9 | 129290 | False | False |
| RSI5m 20-45 | 844 | -78390 | 0.52 | 247 | -32970 | 0.43 | 198 | 5 | -23200 | 0.44 | 1289 | 23 | 49.8 | -134560 | 0.48 | -104.4 | 134160 | False | False |
| RSI5m 0-45 | 859 | -79930 | 0.52 | 247 | -32970 | 0.43 | 198 | 5 | -23200 | 0.44 | 1304 | 23 | 49.8 | -136100 | 0.49 | -104.4 | 135700 | False | False |
| RSI5m 25-50 | 853 | -74770 | 0.53 | 290 | -37730 | 0.43 | 224 | 5 | -23790 | 0.47 | 1367 | 23 | 50.4 | -136290 | 0.5 | -99.7 | 136490 | False | False |
| RSI5m 20-50 | 878 | -78800 | 0.52 | 290 | -37730 | 0.43 | 228 | 5 | -24630 | 0.46 | 1396 | 23 | 50.2 | -141160 | 0.49 | -101.1 | 141360 | False | False |
| RSI5m 0-50 | 893 | -80340 | 0.53 | 290 | -37730 | 0.43 | 228 | 5 | -24630 | 0.46 | 1411 | 23 | 50.3 | -142700 | 0.49 | -101.1 | 142900 | False | False |
| RSI filter off (gap-up band kept) | 914 | -82860 | 0.52 | 297 | -39170 | 0.42 | 242 | 5 | -25150 | 0.46 | 1453 | 23 | 50.2 | -147180 | 0.49 | -101.3 | 147380 | False | False |

## RSI band (gap-up open)

| Cell | train_N | train_Net | train_PF | val_N | val_Net | val_PF | hold_N | hold_Days | hold_Net | hold_PF | all_N | all_Days | all_TgtPct | all_Net | all_PF | all_Exp | all_MaxDD | BeatsLive_all3 | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Gap-up RSI5m 40-50 | 520 | -40840 | 0.57 | 143 | -15310 | 0.51 | 114 | 4 | -12680 | 0.45 | 777 | 21 | 52.4 | -68830 | 0.54 | -88.6 | 69830 | True | False |
| Gap-up RSI5m 28-38 | 492 | -44770 | 0.52 | 181 | -16200 | 0.54 | 113 | 4 | -10340 | 0.52 | 786 | 19 | 52.1 | -71310 | 0.52 | -90.7 | 71530 | False | False |
| Gap-up override off (normal band on gap-up) | 492 | -44770 | 0.52 | 181 | -16200 | 0.54 | 113 | 4 | -10340 | 0.52 | 786 | 19 | 52.1 | -71310 | 0.52 | -90.7 | 71530 | False | False |
| Gap-up RSI5m 40-70 | 541 | -42420 | 0.57 | 145 | -15470 | 0.51 | 127 | 4 | -14460 | 0.43 | 813 | 21 | 52.4 | -72350 | 0.53 | -89.0 | 72750 | False | False |
| Gap-up RSI5m 35-60 | 599 | -50850 | 0.54 | 179 | -15840 | 0.56 | 148 | 4 | -15860 | 0.46 | 926 | 21 | 52.3 | -82550 | 0.53 | -89.1 | 82450 | False | False |
| Gap-up RSI5m 30-50 | 614 | -51440 | 0.54 | 216 | -17870 | 0.57 | 152 | 4 | -15020 | 0.49 | 982 | 21 | 52.7 | -84330 | 0.54 | -85.9 | 84430 | False | False |

## PCR band

| Cell | train_N | train_Net | train_PF | val_N | val_Net | val_PF | hold_N | hold_Days | hold_Net | hold_PF | all_N | all_Days | all_TgtPct | all_Net | all_PF | all_Exp | all_MaxDD | BeatsLive_all3 | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| PCR 0.95-1.0 | 67 | -7500 | 0.54 | 16 | -1540 | 0.52 | 15 | 3 | -200 | 0.9 | 98 | 15 | 51.1 | -9240 | 0.57 | -94.3 | 8840 | True | False |
| PCR 1.1-1.2 | 163 | -10340 | 0.63 | 36 | -1660 | 0.75 | 32 | 3 | -4000 | 0.42 | 231 | 18 | 53.7 | -16000 | 0.61 | -69.3 | 16400 | True | False |
| PCR 1.1-1.3 | 238 | -11470 | 0.69 | 53 | -1540 | 0.81 | 39 | 4 | -4110 | 0.44 | 330 | 19 | 57.8 | -17120 | 0.67 | -51.9 | 17520 | True | False |
| PCR 0.9-1.0 | 114 | -10790 | 0.59 | 26 | -2540 | 0.55 | 35 | 3 | -5100 | 0.4 | 175 | 16 | 50.6 | -18430 | 0.55 | -105.3 | 18030 | True | False |
| PCR 1.1-1.4 | 270 | -12830 | 0.68 | 74 | -5170 | 0.65 | 48 | 4 | -4070 | 0.5 | 392 | 19 | 58.0 | -22070 | 0.65 | -56.3 | 22470 | True | False |
| PCR 1.05-1.2 | 235 | -14920 | 0.63 | 57 | -7020 | 0.46 | 54 | 4 | -4640 | 0.52 | 346 | 20 | 52.0 | -26580 | 0.58 | -76.8 | 27140 | True | False |
| PCR 1.1-none | 287 | -13990 | 0.68 | 151 | -8540 | 0.69 | 51 | 4 | -4150 | 0.52 | 489 | 19 | 58.2 | -26680 | 0.66 | -54.6 | 27080 | True | False |
| PCR 1.1-1.5 | 284 | -14030 | 0.67 | 104 | -9270 | 0.58 | 50 | 4 | -4350 | 0.49 | 438 | 19 | 57.3 | -27650 | 0.62 | -63.1 | 28050 | True | False |
| PCR 1.05-1.3 | 306 | -16570 | 0.66 | 74 | -6900 | 0.52 | 60 | 4 | -4870 | 0.52 | 440 | 20 | 55.1 | -28340 | 0.61 | -64.4 | 28820 | True | False |
| PCR 0.8-1.0 | 177 | -17630 | 0.56 | 34 | -3160 | 0.57 | 48 | 3 | -8280 | 0.36 | 259 | 16 | 49.2 | -29070 | 0.52 | -112.2 | 28670 | True | False |
| PCR 0.7-1.0 | 192 | -20260 | 0.53 | 42 | -3360 | 0.6 | 53 | 3 | -9280 | 0.34 | 287 | 16 | 48.3 | -32900 | 0.5 | -114.6 | 32500 | True | False |
| PCR 1.05-1.4 | 334 | -17890 | 0.66 | 95 | -10530 | 0.5 | 69 | 4 | -4830 | 0.56 | 498 | 20 | 55.4 | -33250 | 0.61 | -66.8 | 33730 | True | False |
| PCR none-1.0 | 192 | -20260 | 0.53 | 47 | -4760 | 0.53 | 55 | 3 | -8970 | 0.37 | 294 | 16 | 48.0 | -33990 | 0.5 | -115.6 | 33590 | True | False |
| PCR 1.05-none | 350 | -18770 | 0.65 | 172 | -13900 | 0.59 | 72 | 4 | -4910 | 0.57 | 594 | 20 | 56.1 | -37580 | 0.62 | -63.3 | 38060 | True | False |
| PCR 1.05-1.5 | 347 | -18810 | 0.65 | 125 | -14630 | 0.49 | 71 | 4 | -5110 | 0.55 | 543 | 20 | 55.2 | -38550 | 0.59 | -71.0 | 39030 | True | False |
| PCR 0.95-1.2 | 352 | -28230 | 0.57 | 80 | -8220 | 0.51 | 77 | 4 | -7340 | 0.49 | 509 | 21 | 52.2 | -43790 | 0.55 | -86.0 | 43990 | True | False |
| PCR 0.95-1.3 | 418 | -30480 | 0.59 | 96 | -8220 | 0.55 | 82 | 4 | -7290 | 0.5 | 596 | 21 | 54.0 | -45990 | 0.57 | -77.2 | 46390 | True | False |
| PCR 0.95-1.4 | 446 | -31800 | 0.59 | 117 | -11850 | 0.52 | 91 | 4 | -7250 | 0.53 | 654 | 21 | 54.4 | -50900 | 0.57 | -77.8 | 51300 | True | False |
| PCR 0.9-1.2 | 390 | -29590 | 0.59 | 88 | -9420 | 0.51 | 95 | 4 | -11900 | 0.41 | 573 | 21 | 52.0 | -50910 | 0.54 | -88.8 | 51110 | True | False |
| PCR 0.9-1.3 | 453 | -32560 | 0.6 | 104 | -9420 | 0.54 | 100 | 4 | -11850 | 0.41 | 657 | 21 | 53.4 | -53830 | 0.56 | -81.9 | 54230 | True | False |
| PCR 0.95-none | 462 | -32680 | 0.59 | 194 | -15220 | 0.6 | 94 | 4 | -7330 | 0.54 | 750 | 21 | 55.1 | -55230 | 0.59 | -73.6 | 55630 | True | False |
| PCR 0.95-1.5 | 459 | -32720 | 0.59 | 147 | -15950 | 0.51 | 93 | 4 | -7530 | 0.52 | 699 | 21 | 54.3 | -56200 | 0.56 | -80.4 | 56600 | False | False |
| PCR 0.9-1.4 | 481 | -33880 | 0.6 | 125 | -13050 | 0.52 | 109 | 4 | -11810 | 0.44 | 715 | 21 | 53.8 | -58740 | 0.56 | -82.2 | 59140 | True | False |
| PCR 0.8-1.2 | 437 | -36600 | 0.56 | 95 | -10240 | 0.51 | 106 | 4 | -13860 | 0.4 | 638 | 21 | 51.0 | -60700 | 0.52 | -95.1 | 60900 | True | False |
| PCR 0.8-1.3 | 499 | -38750 | 0.58 | 111 | -10240 | 0.54 | 111 | 4 | -13810 | 0.41 | 721 | 21 | 52.5 | -62800 | 0.54 | -87.1 | 63200 | True | False |
| PCR 0.9-none | 497 | -34760 | 0.6 | 202 | -16420 | 0.59 | 112 | 4 | -11890 | 0.45 | 811 | 21 | 54.5 | -63070 | 0.57 | -77.8 | 63470 | False | False |
| PCR 0.9-1.5 | 494 | -34800 | 0.6 | 155 | -17150 | 0.51 | 111 | 4 | -12090 | 0.44 | 760 | 21 | 53.8 | -64040 | 0.55 | -84.3 | 64440 | False | False |
| PCR 0.7-1.2 | 450 | -39070 | 0.55 | 103 | -10440 | 0.52 | 111 | 4 | -14860 | 0.39 | 664 | 21 | 50.5 | -64370 | 0.52 | -96.9 | 64570 | False | False |
| PCR none-1.2 | 450 | -39070 | 0.55 | 108 | -11840 | 0.5 | 113 | 4 | -14550 | 0.41 | 671 | 21 | 50.4 | -65460 | 0.51 | -97.6 | 65660 | False | False |
| PCR 0.7-1.3 | 512 | -41220 | 0.57 | 119 | -10440 | 0.55 | 116 | 4 | -14810 | 0.4 | 747 | 21 | 52.1 | -66470 | 0.53 | -89.0 | 66870 | False | False |
| PCR none-1.3 | 512 | -41220 | 0.57 | 124 | -11840 | 0.52 | 118 | 4 | -14500 | 0.41 | 754 | 21 | 51.9 | -67560 | 0.53 | -89.6 | 67960 | False | False |
| PCR 0.8-1.4 | 527 | -40070 | 0.58 | 132 | -13870 | 0.52 | 120 | 4 | -13770 | 0.43 | 779 | 21 | 52.9 | -67710 | 0.54 | -86.9 | 68110 | True | False |
| PCR 0.7-1.4 | 540 | -42540 | 0.57 | 140 | -14070 | 0.53 | 125 | 4 | -14770 | 0.42 | 805 | 21 | 52.5 | -71380 | 0.53 | -88.7 | 71780 | False | False |
| PCR 0.8-none | 543 | -40950 | 0.58 | 209 | -17240 | 0.58 | 123 | 4 | -13850 | 0.44 | 875 | 21 | 53.7 | -72040 | 0.56 | -82.3 | 72440 | False | False |
| SENSEX PCR >= 0.70 off | 541 | -42420 | 0.57 | 145 | -15470 | 0.51 | 128 | 4 | -14740 | 0.43 | 814 | 21 | 52.4 | -72630 | 0.53 | -89.2 | 73030 | False | False |
| PCR 0.8-1.5 | 540 | -40990 | 0.58 | 162 | -17970 | 0.51 | 122 | 4 | -14050 | 0.43 | 824 | 21 | 52.9 | -73010 | 0.54 | -88.6 | 73410 | False | False |
| PCR 0.7-none | 556 | -43420 | 0.57 | 217 | -17440 | 0.59 | 128 | 4 | -14850 | 0.42 | 901 | 21 | 53.3 | -75710 | 0.55 | -84.0 | 76110 | False | False |
| PCR 0.7-1.5 | 553 | -43460 | 0.57 | 170 | -18170 | 0.52 | 127 | 4 | -15050 | 0.42 | 850 | 21 | 52.5 | -76680 | 0.53 | -90.2 | 77080 | False | False |
| PCR none-none | 556 | -43420 | 0.57 | 222 | -18840 | 0.57 | 130 | 4 | -14540 | 0.44 | 908 | 21 | 53.2 | -76800 | 0.55 | -84.6 | 77200 | False | False |
| PCR filter off | 556 | -43420 | 0.57 | 222 | -18840 | 0.57 | 130 | 4 | -14540 | 0.44 | 908 | 21 | 53.2 | -76800 | 0.55 | -84.6 | 77200 | False | False |
| PCR none-1.5 | 553 | -43460 | 0.57 | 175 | -19570 | 0.5 | 129 | 4 | -14740 | 0.43 | 857 | 21 | 52.4 | -77770 | 0.53 | -90.7 | 78170 | False | False |

## ATR min

| Cell | train_N | train_Net | train_PF | val_N | val_Net | val_PF | hold_N | hold_Days | hold_Net | hold_PF | all_N | all_Days | all_TgtPct | all_Net | all_PF | all_Exp | all_MaxDD | BeatsLive_all3 | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| ATR% >= 0.05 | 229 | -20770 | 0.55 | 84 | -8140 | 0.57 | 73 | 4 | -8200 | 0.47 | 386 | 17 | 53.0 | -37110 | 0.54 | -96.1 | 37510 | True | False |
| ATR% >= 0.04 | 409 | -32290 | 0.58 | 118 | -12500 | 0.53 | 82 | 4 | -8490 | 0.5 | 609 | 20 | 53.7 | -53280 | 0.56 | -87.5 | 53680 | True | False |
| ATR% >= 0.02 | 560 | -43540 | 0.57 | 146 | -15350 | 0.51 | 133 | 4 | -14540 | 0.44 | 839 | 21 | 52.7 | -73430 | 0.54 | -87.5 | 73830 | False | False |
| ATR filter off | 569 | -43740 | 0.57 | 148 | -16150 | 0.5 | 134 | 4 | -14060 | 0.46 | 851 | 21 | 52.7 | -73950 | 0.54 | -86.9 | 74350 | False | False |

## EarlyTrendGate

| Cell | train_N | train_Net | train_PF | val_N | val_Net | val_PF | hold_N | hold_Days | hold_Net | hold_PF | all_N | all_Days | all_TgtPct | all_Net | all_PF | all_Exp | all_MaxDD | BeatsLive_all3 | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Require 5m EMA20>EMA50 | 148 | -8660 | 0.64 | 52 | -5390 | 0.49 | 73 | 3 | -6780 | 0.48 | 273 | 17 | 54.9 | -20830 | 0.57 | -76.3 | 21230 | True | False |
| EarlyTrendGate on (block when 5m EMA20>EMA50) | 394 | -34500 | 0.54 | 93 | -10080 | 0.52 | 54 | 3 | -7680 | 0.38 | 541 | 16 | 51.1 | -52260 | 0.52 | -96.6 | 52660 | True | False |
| Require 5m EMA20<EMA50 (5m aligned) | 394 | -34500 | 0.54 | 93 | -10080 | 0.52 | 54 | 3 | -7680 | 0.38 | 541 | 16 | 51.1 | -52260 | 0.52 | -96.6 | 52660 | True | False |

## LTP vs EMA20

| Cell | train_N | train_Net | train_PF | val_N | val_Net | val_PF | hold_N | hold_Days | hold_Net | hold_PF | all_N | all_Days | all_TgtPct | all_Net | all_PF | all_Exp | all_MaxDD | BeatsLive_all3 | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| LTP vs EMA20 off | 647 | -44840 | 0.6 | 197 | -18110 | 0.54 | 160 | 5 | -22600 | 0.36 | 1004 | 22 | 52.8 | -85550 | 0.54 | -85.2 | 86350 | False | False |

## EMA gap (live: 10, BANKNIFTY 15)

| Cell | train_N | train_Net | train_PF | val_N | val_Net | val_PF | hold_N | hold_Days | hold_Net | hold_PF | all_N | all_Days | all_TgtPct | all_Net | all_PF | all_Exp | all_MaxDD | BeatsLive_all3 | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| |EMA20-EMA50| > 30 pts (all symbols) | 385 | -26110 | 0.55 | 95 | -10790 | 0.45 | 75 | 4 | -9730 | 0.34 | 555 | 18 | 51.9 | -46630 | 0.49 | -84.0 | 47030 | True | False |
| |EMA20-EMA50| > 20 pts (all symbols) | 451 | -31530 | 0.56 | 122 | -14470 | 0.45 | 100 | 4 | -12860 | 0.39 | 673 | 20 | 52.0 | -58860 | 0.51 | -87.5 | 59260 | True | False |
| |EMA20-EMA50| > 15 pts (all symbols) | 495 | -39330 | 0.54 | 137 | -17000 | 0.44 | 116 | 4 | -12370 | 0.45 | 748 | 21 | 51.9 | -68700 | 0.5 | -91.8 | 69100 | False | False |
| |EMA20-EMA50| > 10 pts (all symbols) | 554 | -43640 | 0.57 | 149 | -15870 | 0.51 | 137 | 4 | -16660 | 0.41 | 840 | 21 | 52.1 | -76170 | 0.53 | -90.7 | 76770 | False | False |
| |EMA20-EMA50| > 5 pts (all symbols) | 590 | -45880 | 0.58 | 159 | -17640 | 0.49 | 154 | 4 | -17730 | 0.43 | 903 | 21 | 52.0 | -81250 | 0.53 | -90.0 | 81850 | False | False |
| EMA gap off | 620 | -48490 | 0.57 | 170 | -16200 | 0.54 | 172 | 4 | -19320 | 0.44 | 962 | 21 | 52.5 | -84010 | 0.54 | -87.3 | 84610 | False | False |

## RSI x PCR (joint)

| Cell | train_N | train_Net | train_PF | val_N | val_Net | val_PF | hold_N | hold_Days | hold_Net | hold_PF | all_N | all_Days | all_TgtPct | all_Net | all_PF | all_Exp | all_MaxDD | BeatsLive_all3 | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| RSI5m 28-35 & PCR none-1.0 | 169 | -17830 | 0.54 | 41 | -4120 | 0.54 | 46 | 3 | -7260 | 0.33 | 256 | 16 | 48.1 | -29210 | 0.5 | -114.1 | 28810 | True | False |
| RSI5m 28-38 & PCR none-1.0 | 192 | -20260 | 0.53 | 47 | -4760 | 0.53 | 55 | 3 | -8970 | 0.37 | 294 | 16 | 48.0 | -33990 | 0.5 | -115.6 | 33590 | True | False |
| RSI5m 28-40 & PCR none-1.0 | 216 | -24830 | 0.51 | 49 | -4810 | 0.53 | 57 | 3 | -8010 | 0.43 | 322 | 16 | 47.8 | -37650 | 0.5 | -116.9 | 37450 | True | False |
| RSI5m 25-38 & PCR none-1.0 | 211 | -26360 | 0.47 | 49 | -5560 | 0.49 | 56 | 3 | -9250 | 0.36 | 316 | 16 | 45.8 | -41170 | 0.45 | -130.3 | 40770 | True | False |
| RSI5m 30-45 & PCR none-1.0 | 247 | -30590 | 0.46 | 51 | -4850 | 0.53 | 54 | 4 | -6920 | 0.47 | 352 | 18 | 46.5 | -42360 | 0.47 | -120.3 | 41960 | True | False |
| RSI5m 28-35 & PCR none-1.2 | 361 | -30430 | 0.57 | 80 | -9550 | 0.47 | 96 | 4 | -11960 | 0.39 | 537 | 21 | 50.2 | -51940 | 0.52 | -96.7 | 52140 | True | False |
| RSI5m 28-35 & PCR 0.8-none | 432 | -32780 | 0.58 | 155 | -13300 | 0.58 | 104 | 4 | -12040 | 0.4 | 691 | 21 | 53.1 | -58120 | 0.56 | -84.1 | 58320 | True | False |
| RSI5m 28-35 & PCR none-1.4 | 429 | -34450 | 0.57 | 108 | -11820 | 0.52 | 107 | 4 | -12370 | 0.41 | 644 | 21 | 51.9 | -58640 | 0.53 | -91.1 | 58840 | True | False |
| RSI5m 28-38 & PCR none-1.2 | 450 | -39070 | 0.55 | 108 | -11840 | 0.5 | 113 | 4 | -14550 | 0.41 | 671 | 21 | 50.4 | -65460 | 0.51 | -97.6 | 65660 | False | False |
| RSI5m 28-38 & PCR 0.8-none | 543 | -40950 | 0.58 | 209 | -17240 | 0.58 | 123 | 4 | -13850 | 0.44 | 875 | 21 | 53.7 | -72040 | 0.56 | -82.3 | 72440 | False | False |
| RSI5m 25-38 & PCR none-1.2 | 499 | -45700 | 0.53 | 110 | -13940 | 0.45 | 115 | 4 | -14630 | 0.41 | 724 | 21 | 49.5 | -74270 | 0.5 | -102.6 | 74470 | False | False |
| RSI5m 28-40 & PCR none-1.2 | 507 | -45030 | 0.54 | 119 | -13990 | 0.47 | 132 | 4 | -16250 | 0.43 | 758 | 21 | 50.2 | -75270 | 0.51 | -99.3 | 75470 | False | False |
| RSI5m 25-38 & PCR 0.8-none | 607 | -47620 | 0.57 | 219 | -19120 | 0.57 | 128 | 4 | -14530 | 0.44 | 954 | 21 | 53.1 | -81270 | 0.55 | -85.2 | 81670 | False | False |
| RSI5m 25-38 & PCR none-1.4 | 601 | -49970 | 0.55 | 153 | -17570 | 0.5 | 130 | 4 | -14940 | 0.43 | 884 | 21 | 51.5 | -82480 | 0.52 | -93.3 | 82880 | False | False |
| RSI5m 30-45 & PCR none-1.2 | 580 | -51610 | 0.53 | 142 | -15710 | 0.48 | 152 | 5 | -17330 | 0.47 | 874 | 23 | 50.4 | -84650 | 0.51 | -96.9 | 84650 | False | False |
| RSI5m 28-40 & PCR none-1.4 | 609 | -50280 | 0.55 | 164 | -19940 | 0.46 | 146 | 4 | -16160 | 0.45 | 919 | 22 | 51.6 | -86380 | 0.52 | -94.0 | 86780 | False | False |
| RSI5m 28-40 & PCR 0.8-none | 613 | -49090 | 0.56 | 253 | -23580 | 0.54 | 143 | 4 | -15950 | 0.45 | 1009 | 22 | 52.6 | -88620 | 0.54 | -87.8 | 89020 | False | False |
| RSI5m 30-45 & PCR none-1.4 | 685 | -56900 | 0.55 | 232 | -29870 | 0.43 | 184 | 5 | -20020 | 0.47 | 1101 | 23 | 50.8 | -106790 | 0.5 | -97.0 | 106390 | False | False |
| RSI5m 30-45 & PCR 0.8-none | 691 | -55640 | 0.56 | 347 | -35360 | 0.5 | 182 | 5 | -20090 | 0.46 | 1220 | 23 | 51.8 | -111090 | 0.52 | -91.1 | 110690 | False | False |

