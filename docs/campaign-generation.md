# Campaign generation

## Campaign brief wizard

The parent-led wizard collects a title, genre, tone, optional story idea,
campaign length in sessions, session length in minutes, and optional
inclusions and exclusions. It saves a `CampaignBrief` draft independently of
campaigns; saving a brief does not create a playable campaign or start AI
generation. Saved briefs can be reopened and edited from the campaign library,
or discarded there. Leaving the editor without saving discards the in-progress
changes.

The API owns brief persistence. `GET /api/campaign-briefs` lists saved drafts,
`POST /api/campaign-briefs` creates one, `GET
/api/campaign-briefs/{briefId}` loads one, `PUT
/api/campaign-briefs/{briefId}` updates it, and `DELETE
/api/campaign-briefs/{briefId}` discards it. The API validates required text,
length bounds, list sizes, and optional idea size before writing to SQLite.
Inclusions and exclusions are stored as JSON lists in the brief row.

The server supplies the fixed safety boundaries on every new or updated brief:
no gore or cruelty, no mature themes, no permanent character death, no
mandatory tactical combat, and low-fright, age-appropriate content. These
cannot be removed by browser input. The wizard displays them separately from
the parent's optional exclusions.

Campaign length is bounded to 1-30 sessions and session length to 15-180
minutes. The wizard currently offers common choices of 3, 6, or 10 sessions
and 30, 45, 60, or 90 minutes. AI generation, approval, and conversion of a
brief into a playable campaign are later workflow steps. The campaign library
also retains a separate option to create an empty world without a brief.
