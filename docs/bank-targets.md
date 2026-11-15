# Bank Targets — تنفيذ محلي

## الوصول وطريقة الاستخدام

- من قائمة **Collections → Score → Bank Targets / مستهدفات البنوك**، أو مسار `/collections/score/targets`.
- تشغيل الواجهة فقط: `cd D:\Mis_v2\frontend` ثم `npm run dev`. العنوان المعتاد `http://localhost:5173/collections/score/targets`؛ استخدم البورت اللي Vite يطبعه لو مختلف.
- الواجهة تستخدم `VITE_API_URL`، والافتراضي `http://localhost:5000/api`. استخدم API محلي مُجهّز، مش عنوان الإنتاج، لو هتجرب الحفظ.
- اختار البنك أو شركة التمويل، نوع ACT/WO، المحفظة والشهر. الجهة والمحافظ بتتحمل من APIs المشروع الحالية، بنفس نطاق الوصول.
- مفيش أهداف افتراضية بتتطبق عند القراءة. اختار إنشاء مسودة فاضية أو استخدام القالب المرجعي مع تأكيد صريح.
- القالب بيحفظ الـ14 صف بنفس قيم الطلب، وإجمالي أوزانه الصحيح **100.00**، وBKT More بنسبة 4% ووزن 100، وتوزيع ACTIVE/MORE بقيم 90%/10%، وقواعد W-Off بقيم 20%/40%.
- الحفظ مسودة، وبعده مراجعة، وبعدها اعتماد. أي تعديل بعد المراجعة بيرجع الحالة مسودة ويلغي المراجعة. النسخة المعتمدة مقفولة، ومفيش endpoint لحذفها أو استبدالها.
- المراجعة والاعتماد يتطلبوا إجمالي أوزان كمية 100، وإجمالي توزيع 100، ووزن MORE يساوي 100، ومستهدفات موجبة. المسودة ممكن تحفظ أوزان إجماليها غير صحيح، لكن مش هتتراجع أو تعتمد قبل التصحيح.
- كل الأرقام من 0 إلى 100 وبحد أقصى منزلتين عشريتين. الفراغ مش بيتحول لصفر، وحقول JSON الرقمية الناقصة مرفوضة.
- نسخ الشهر السابق بيستخدم الشهر التقويمي السابق مباشرة لنفس الجهة والمحفظة والنوع، بشرط يكون معتمد. بيعمل مسودة جديدة، من غير نسخ الاعتماد أو المراجع. النسخ لا يستبدل إعداد موجود في الشهر المطلوب.
- سجل الصفحة بيعرض آخر 100 عملية، ومعاها القيم المحفوظة والمستخدم والتوقيت بتوقيت القاهرة. بيانات المستخدم في السجل بمعرّفه؛ لا يتم اختراع أسماء.

## الصلاحيات والحماية

- العرض تحت سياسة `CollectionsAccess` وبنفس فحص نطاق الجهة والمحفظة الموجود في Score.
- الحفظ والنسخ تحت `CollectionsConfigurationManage`، مع إعادة التحقق داخل الخدمة من صلاحية الإعداد أو أدوار Admin/CollectionsOperationsManager.
- المراجعة والاعتماد لأدوار Admin وCollectionsOperationsManager وCollectionsReviewer. لا يوجد افتراض جديد بضرورة أن المراجع والمُعدّ شخصان مختلفان؛ لو ده مطلوب تجاريًا يحتاج قرار صريح.
- `Version` من نوع UUID مستخدم كـconcurrency token. الحفظ والمراجعة والاعتماد بيشترطوا النسخة المقروءة، وبيظهر تعارض واضح لو حد سبق وعدّلها.
- الفهرس الفريد يمنع وجود إعدادين لنفس `(OrganizationId, PortfolioId, PortfolioType, ReportingPeriod)`، بما فيها محاولات الإنشاء المتزامنة.
- حفظ الإعداد وسجل العملية بيتم في نفس `SaveChanges`، مع معاملة EF الافتراضية في PostgreSQL.
- مفيش كتابة في `CollectionScoreValues` أو `ActScorePeriods` أو حالات العملاء أو المدفوعات.

## علاقة الصفحة بالتقييم

- محرك `ActScoreEngine` و`CollectionScoreCalculator` لم تتغير معادلاتهم.
- لوحة Score Dashboard تحتفظ بنفس الشكل؛ التعديل فيها لدعم رابط `reportingPeriod` فقط، حتى لو الشهر لسه ملوش نتائج.
- صفحة Bank Targets لا تعرض نتائج عند غياب المستهدفات أو عدم اعتمادها.
- بعد الاعتماد، النتائج تُقرأ فقط إذا ACT مفعّل، والمصدر موجود لنفس الجهة والمحفظة والشهر والعملة، ولا يوجد تقييم يدوي محفوظ، وقواعد المصدر تطابق كل مستهدف ووزن كمي وتوزيع ACTIVE/MORE وMORE target.
- لو الشروط متحققة، الحساب يستدعي محرك ACT الموجود بدون تغيير قواعده أو بياناته. عمود Score يعرض `BalanceScore` و`MoreScore` قبل أوزان التوزيع، وليس أرقامًا وهمية أو أصفارًا بديلة للقيم المفقودة.
- لو المصدر ناقص أو مختلف، الصفحة تعرض رسالة سبب وعدم توفر النتيجة. الاعتماد هنا لا يعيد إنشاء مصادر ACT ولا يعيد احتساب تقييمات تاريخية مخزنة.
- الربط التلقائي المستقبلي: يحتاج مسار مُراجع يجهز مصدر ACT immutable من الإعداد المعتمد، بعد التحقق من ملف Excel والقواعد الإضافية. لم يتم تفعيل هذا المسار أو إجبار لوحة Score القديمة على استخدام الجدول الجديد.

## القواعد غير المحسومة

- حالة W-Off عند **90% بالضبط** غير معرّفة. الحساب غير مفعّل بالكامل، ولا يوجد زر لتفعيله أو افتراض أن 20% و40% عمولات.
- أهداف BKT4 وBKT5 تجمع 105%؛ استقرار STAB الناتج يكون سالبًا. محرك ACT يشترط موافقة صريحة على ذلك داخل قواعد المصدر، ولم يتم إضافة هذه الموافقة تلقائيًا.
- الصورة وملف Excel الأصلي لم يكونا متاحين في هذه المهمة. القالب مبني على الجدول النصي في الطلب؛ تمت مراجعة كود ACT وتوثيق Excel الموجود وملف اختبار `act-score-reference.json` فقط.

## قاعدة البيانات والنشر — لم يتم التنفيذ

Migration المصدر: `20261115204454_AddBankTargets`.

1. جدول `BankTargetConfigurations`: النطاق والشهر، حالة DRAFT/REVIEWED/APPROVED، JSON للقيم، النسخة، منشئ/معدل/مراجع/معتمد مع التوقيت، ومعرّف الإعداد المنسوخ منه.
2. جدول `BankTargetAudits`: نسخة القيم والحالة والنسخة UUID لكل عملية مع المستخدم والتوقيت.

العلاقات للجهات والمحافظ والمستخدمين Restrict. علاقة المحفظة والجهة مركبة لمنع ربط محفظة بجهة مختلفة. لا يتضمن `Up` حذفًا أو تعديلًا لبيانات مالية؛ إضافة جدولين وفهارس وعلاقات فقط. `Down` يحذف الجدولين وبالتالي مش مناسب للاستخدام بعد حفظ تاريخ فعلي بدون خطة معتمدة.

**المطلوب قبل تجربة API الحقيقية:** مراجعة الـmigration وتطبيقها يدويًا بتفويض مستقل على قاعدة مناسبة، ثم تشغيل backend محدث. ده لم يتم في المهمة. تجهيز الملفات تم بأداة EF مع عنوان قاعدة وهمي غير قابل للاتصال، ولم يُستخدم `database update`.

**تنبيه تشغيل مرتبط بالكود الموجود:** البرنامج الحالي بيستدعي `MigrateAsync` وقت الإقلاع، وبيعمل seeding في Development. علشان شرط عدم تغيير البيانات، لم أشغّل الـAPI. عند تجهيز بيئة اختبار لاحقًا، بعد تطبيق المخطط يدويًا ووجود JWT signing key بالفعل، وضع `MIS__SkipStartupDatabaseChanges=true` يمنع migrations/seeding والـworker الكاتب عند التشغيل. وضع التشغيل الآمن لا يجهز جداول مفقودة ولا يمنع كتابة endpoints لو المستخدم اختار الحفظ.

لم يتم تعديل إعداد التشغيل الافتراضي أو تنفيذ Push أو Deploy. الواجهة ممكن تتفتح قبل تجهيز المخطط، لكن الحفظ والقراءة الفعلية للميزة لن ينجحا بدون الجدولين.

## التحقق

- Release backend build: نجح.
- 90 اختبار backend: BankTargets lifecycle/scope/permissions/concurrency/JSON + اختبارات Score السابقة + تطابق model/migration snapshot، كلها نجحت. بيانات الخدمات في EF InMemory فقط؛ اختبار المخطط يفحص metadata بدون اتصال.
- 11 اختبار Node للمدخلات والأرقام والتواريخ ورسائل العربية/الإنجليزية وانحدارات رسائل Score: نجحت.
- `npm run build`: نجح، شامل فحص UI وTypeScript وVite.
- Edge headless: عرض desktop 1440px وmobile 390px بالعربية RTL والإنجليزية LTR؛ اختيار القالب، القيم الفارغة، إجمالي غير صحيح، الحفظ والمراجعة والاعتماد وقفل الأزرار. كل طلبات API intercepted ببيانات اختبار وهمية داخل متصفح مستقل؛ مفيش اتصال بـAPI أو قاعدة بيانات.
- تحذيرات البناء الموجودة: `System.Security.Cryptography.Pkcs 8.0.1` وتحذير Vite لحزمة أكبر من 500kB؛ لم يتم تغيير dependencies الخاصة بالمشروع.
- لم يتم اختبار SQL فعلي على PostgreSQL أو تكامل حي مع الاستضافة، التزامًا بعدم تعديل البيانات. التحقق من قيود relational على قاعدة فعلية يظل خطوة نشر/اختبار منفصلة.

أوامر إعادة الاختبارات الآمنة:

```powershell
cd D:\Mis_v2\backend
dotnet test MIS.Domain.Tests/MIS.Domain.Tests.csproj --configuration Release --no-restore -m:1 -nr:false --filter "FullyQualifiedName~BankTargets|FullyQualifiedName~DatabaseMigrationTests|FullyQualifiedName~Score"
cd D:\Mis_v2\frontend
node --test scripts/bank-targets.test.cjs scripts/score-calculation.test.cjs
npm run build
```

لقطات واختبار المتصفح محليًا في `_buildcheck/bank-targets-browser/` (مجلد متجاهَل في Git، بيانات اختبار فقط).

## الملفات الجديدة

- `backend/MIS.API/Controllers/BankTargetsController.cs`
- `backend/MIS.Application/DTOs/Collections/BankTargetDtos.cs`
- `backend/MIS.Application/Interfaces/IBankTargetsService.cs`
- `backend/MIS.Application/Scoring/BankTargetPolicy.cs`
- `backend/MIS.Domain/Entities/BankTargetConfiguration.cs`
- `backend/MIS.Infrastructure/Persistence/Configurations/BankTargetConfigurationMapping.cs`
- `backend/MIS.Infrastructure/Services/CollectionScoreService.Targets.cs`
- `backend/MIS.Infrastructure/Persistence/Migrations/20261115204454_AddBankTargets.cs`
- `backend/MIS.Infrastructure/Persistence/Migrations/20261115204454_AddBankTargets.Designer.cs`
- `backend/MIS.Domain.Tests/BankTargetsTests.cs`
- `frontend/src/pages/collections/BankTargetsPage.tsx`
- `frontend/src/features/collections/types/bankTargets.ts`
- `frontend/src/features/collections/services/bankTargetsService.ts`
- `frontend/src/features/collections/bankTargetDraft.ts`
- `frontend/src/features/collections/localization/bankTargetsText.ts`
- `frontend/scripts/bank-targets.test.cjs`
- `docs/bank-targets.md`

## الملفات المعدلة

- `backend/MIS.Infrastructure/DependencyInjection.cs`: تسجيل خدمة المستهدفات.
- `backend/MIS.Infrastructure/Persistence/ApplicationDbContext.cs`: DbSets جديدة.
- `backend/MIS.Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs`: تحديث snapshot.
- `frontend/src/routes/AppRoutes.tsx`: route lazy جديد.
- `frontend/src/components/layout/CollectionsLayout.tsx`: رابط مستهدفات بعد Score.
- `frontend/src/features/collections/localization/collectionsTranslations.ts`: عنوان القائمة باللغتين.
- `frontend/src/pages/collections/CollectionsScorePage.tsx`: رابط من صفحة اختيار التقييم للمستهدفات.
- `frontend/src/pages/collections/CollectionsScoreDashboardPage.tsx`: دعم الشهر في الرابط فقط، بدون تعديل العرض أو المعادلات.

## API

الأساس: `/api/collections/bank-targets`.

- `GET /template`: القالب فقط بدون حفظ.
- `GET /{organizationId}/{portfolioType}/{portfolioId}/{period}`: الإعداد والصلاحيات والنتائج المتاحة والتاريخ.
- `PUT /{organizationId}/{portfolioType}/{portfolioId}/{period}`: `{ expectedVersion, values }` لحفظ مسودة؛ النسخة null للإنشاء فقط.
- `POST /{organizationId}/{portfolioType}/{portfolioId}/{period}/copy-previous`: نسخة مسودة جديدة.
- `POST /{organizationId}/{portfolioType}/{portfolioId}/{period}/review`: `{ expectedVersion }`.
- `POST /{organizationId}/{portfolioType}/{portfolioId}/{period}/approve`: `{ expectedVersion }`.

الشهر `YYYY-MM` والنوع ACT أو WO. لا يوجد endpoint لتنفيذ migrations أو تعديل التقييمات المالية أو تفعيل W-Off.
