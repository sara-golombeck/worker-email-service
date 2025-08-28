# Email Worker Tests

יוניטסטים מקצועיים לשירות Email Worker.

## מבנה הטסטים

- **EmailServiceTests** - בדיקת שליחת מיילים דרך AWS SES
- **EmailWorkerServiceTests** - בדיקת עיבוד הודעות מ-SQS  
- **ModelsTests** - בדיקת מודלי הנתונים וסריאליזציה

## הרצת הטסטים

```bash
dotnet test
```