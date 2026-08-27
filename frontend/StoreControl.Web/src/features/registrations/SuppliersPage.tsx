import { permissions } from '../auth/authTypes'
import { PartyRegistrationPage } from './PartyRegistrationPage'
import { createSupplier, listSuppliers, updateSupplier } from './registrationService'

export function SuppliersPage() {
  return (
    <PartyRegistrationPage
      eyebrow="Cadastros"
      title="Fornecedores"
      singular="fornecedor"
      managePermission={permissions.suppliersManage}
      list={listSuppliers}
      create={createSupplier}
      update={updateSupplier}
    />
  )
}
