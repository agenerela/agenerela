# Risk management plan — 29 October 2026

The COMP 490 in-class practice on risk management: each member identifies at least two risks
in the group project, analyses them and plans how to handle them. One reply per group, with
the plan attached as an Excel file. All six members took part.

| File | What it is |
|---|---|
| [`Agenerela-Risk-Management-Plan.xlsx`](Agenerela-Risk-Management-Plan.xlsx) | The submission. **Risk Register**: 14 risks, two per member plus two for the whole team, each with type, affects, impact areas, probability, effects, a calculated score and priority, a what-if question, strategy, planned actions, indicators to watch and status. **Risk Matrix**: open risks counted by probability and effects, and risks per member. **Sprint Review Log**: one line per review. **Legend**: the scales and how to edit |
| [`discussion-reply.md`](discussion-reply.md) | The text posted to the Canvas discussion |
| [`build-risk-plan.py`](build-risk-plan.py) | The script that builds the workbook, with every risk's wording in it |

**Keeping it up to date.** The course asks for the register to be reviewed once per sprint:
change probability, effects and status (Monitoring, Observed, Resolved) on the Risk Register,
set *Last reviewed*, and add a line to the Sprint Review Log. Edit the pale-yellow cells only;
Score and Priority are formulas, and conditional formatting colours the cells. The workbook
opens in Excel or, uploaded to Google Drive, in Google Sheets with its drop-downs and colours.

**Rebuilding it.** Edits made in Excel are the record from now on; rerunning the script
overwrites them. To rebuild from the script (needs `openpyxl`):

```bash
python build-risk-plan.py Agenerela-Risk-Management-Plan.xlsx
```

openpyxl writes formulas without their results, so open the file once in Excel and save it,
or a viewer that does not recalculate shows the Score and Priority columns empty.
