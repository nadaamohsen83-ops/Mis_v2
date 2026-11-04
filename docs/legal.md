# Legal Department

The Legal module has four primary pages: Dashboard, Clients, Upload Clients, and Client Files. Clients referred from Collections are selected from existing, non-archived collection cases with `LEGAL` status. These records are not copied; client profiles and documents remain linked to the original `CollectionCustomer`.

Excel uploads accept XLSX, XLS, and CSV files. Column headers are matched automatically and support Arabic and English client names and addresses, National ID, mobile number, bank or organization, and notes. A bank name must identify one active MIS organization. Rows require a name, bank, and either a National ID or mobile number. The preview identifies existing MIS customers, duplicates, and invalid rows before import. Existing customer data is only filled where values are missing.

Legal client documents are stored through the existing MIS file-storage service and are linked to the customer record. PDF, JPEG, and PNG content is validated; uploads are limited to 10 MB. Legal access controls reads, while the existing Legal management permission controls imports, notes, and document changes.

The additive `AddLegalClientManagement` migration creates legal assignment and document metadata tables. It does not modify or remove collection customers, cases, or existing attachments. Apply the migration through the normal deployment process before using client imports or document uploads.
