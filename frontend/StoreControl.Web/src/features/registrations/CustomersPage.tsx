import { permissions } from '../auth/authTypes'
import { PartyRegistrationPage } from './PartyRegistrationPage'
import { createCustomer, listCustomers, updateCustomer } from './registrationService'

export function CustomersPage() {
  return (
    <PartyRegistrationPage
      eyebrow="Cadastros"
      title="Clientes"
      singular="cliente"
      managePermission={permissions.customersManage}
      list={listCustomers}
      create={createCustomer}
      update={updateCustomer}
    />
  )
}
