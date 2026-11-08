export const equipmentTypes = [
  'Cookware',
  'Bakeware',
  'Knives',
  'Utensils',
  'Appliances',
  'PrepTools',
  'Measuring',
  'Serving',
  'Storage',
  'Barware',
  'Other',
] as const

export const equipmentConditions = ['New', 'Good', 'Fair', 'Poor', 'Damaged'] as const

export type EquipmentType = typeof equipmentTypes[number]
export type EquipmentCondition = typeof equipmentConditions[number]
export type EquipmentAvailability = 'Available' | 'Unavailable'

export type Equipment = {
  id: string
  name: string
  type: EquipmentType
  quantity: number
  brand: string | null
  size: string | null
  notes: string | null
  condition: EquipmentCondition
  location: string | null
  availability: EquipmentAvailability
  createdAtUtc: string
  updatedAtUtc: string
  version: string
}

export type EquipmentUpsertRequest = {
  name: string
  type: EquipmentType
  quantity: number
  condition: EquipmentCondition
  brand: string | null
  size: string | null
  notes: string | null
  location: string | null
}

export type EquipmentPage = {
  items: Equipment[]
  page: number
  pageSize: number
  totalCount: number
}

export type EquipmentFilters = {
  page: number
  pageSize: number
  search?: string
  type?: EquipmentType
  condition?: EquipmentCondition
  availability?: EquipmentAvailability
}
