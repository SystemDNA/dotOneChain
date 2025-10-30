# DynamicFormsRazor (Razor Pages + MongoDB)

A production-ready starter to define forms as a **single JSON document** where each section embeds its fields, store it in MongoDB, and render a wizard-style UI. Submissions are saved as JSON too.

## Features
- .NET 8 Razor Pages, Bootstrap 5 UI
- **Single-document schema**: `FormDefinition` holds `Sections[]`, each `Section` has its own `Fields[]`
- Validation: required, min/max length, regex pattern, numeric range; server-side enforced with `SchemaValidator`
- MongoDB persistence: `form_definitions` (schemas) and `form_submissions` (answers)
- Wizard UI with Previous/Next, step buttons, and final Submit
- Create page: edit one JSON (Sections array with nested Fields)

## Getting Started
1. Ensure MongoDB is running locally (`mongodb://localhost:27017`) or update `appsettings.json`.
2. From the project folder:
   ```bash
   dotnet restore
   dotnet run
   ```
3. Visit https://localhost:5001 (or the shown URL).
4. Create a definition on **Forms → Create** by editing the single JSON box.
5. Open the wizard via **Open Wizard** on the Definitions list.
6. Submit; check `form_submissions` collection for saved data.

## JSON Template (copy into Create page)
```json
[
  {
    "Id": "basic",
    "Title": "Basic Info",
    "Description": "Your basic details.",
    "Order": 1,
    "Fields": [
      { "Name": "name", "Label": "Full Name", "Type": "Text", "Placeholder": "e.g. Rajasekhar D.", "Validation": { "Required": true, "MinLength": 2, "MaxLength": 80 } },
      { "Name": "firstName", "Label": "First Name", "Type": "Text", "Validation": { "Required": true, "MinLength": 2, "MaxLength": 50 } },
      { "Name": "email", "Label": "Email", "Type": "Email", "Validation": { "Required": true } }
    ]
  },
  {
    "Id": "more",
    "Title": "More Details",
    "Description": "Additional information.",
    "Order": 2,
    "Fields": [
      { "Name": "gender", "Label": "Gender", "Type": "Select", "Options": ["Male","Female","Other"], "Validation": { "Required": true } },
      { "Name": "age", "Label": "Age", "Type": "Number", "Validation": { "Min": 18, "Max": 120 } }
    ]
  }
]
```
