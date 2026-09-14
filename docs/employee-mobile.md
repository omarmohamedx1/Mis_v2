# Employee mobile number

Reuses the existing nullable `Employee.MobileNumber` string column (32 characters). No migration or duplicate contact field is needed.

The existing employee create/update requests and details response now include `mobileNumber`. Add/Edit displays the optional field beside National ID. The profile overview displays it, and the existing contact editor preserves supplied country codes for the primary mobile number. Other contact fields retain their existing behavior. The main employee table remains compact.

Blank/whitespace values become null. Entered values are trimmed and validated as phone strings of at most 32 characters. Leading zeros, `+20`, international prefixes and internal separators are preserved. Existing employee search now also searches MobileNumber, alongside its existing name, employee number and National ID matching.

Employee Import accepts MobileNumber and suggests Mobile, Mobile Number, Phone, Phone Number, Telephone, رقم الموبايل, رقم الهاتف and التليفون. The preview displays the mobile number. CSV and Excel text cells retain their leading zeros and country codes. For numeric Excel cells, an explicit all-zero format such as `00000000000` is honored for the mapped mobile column only. Numbers already stripped of leading digits by Excel cannot be reconstructed; the mapping screen explains using Text or an explicit zero-padding format.

Verification includes optional/blank and international values, invalid phone rejection, creation/details, editing/clearing without losing other contacts, CSV/XLSX text and zero-padding, and a disposable PostgreSQL persistence/search test. Set `MIS_MOBILE_TEST_CONNECTION` to run the database test; it creates and drops a uniquely named temporary database. No permanent test employees are required.
