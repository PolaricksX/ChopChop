import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  createEquipment,
  deleteEquipment,
  listEquipment,
  setEquipmentAvailability,
  updateEquipment,
} from './api'
import type { Equipment } from './types'
import { EquipmentTab } from './EquipmentTab'

vi.mock('./api', () => ({
  EquipmentApiError: class EquipmentApiError extends Error {
    readonly status: number
    constructor(message: string, status: number) {
      super(message)
      this.status = status
    }
  },
  createEquipment: vi.fn(),
  deleteEquipment: vi.fn(),
  listEquipment: vi.fn(),
  setEquipmentAvailability: vi.fn(),
  updateEquipment: vi.fn(),
}))

const equipment: Equipment = {
  id: 'item-1',
  name: 'Saucepan',
  type: 'Cookware',
  quantity: 1,
  brand: 'Acme',
  size: '2 qt',
  notes: null,
  condition: 'Good',
  location: 'Top shelf',
  availability: 'Available',
  createdAtUtc: '2026-11-04T12:00:00Z',
  updatedAtUtc: '2026-11-04T12:00:00Z',
  version: 'AQIDBAUGBwg=',
}

describe('EquipmentTab', () => {
  beforeEach(() => {
    vi.mocked(listEquipment).mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 20,
      totalCount: 0,
    })
    vi.mocked(createEquipment).mockResolvedValue(equipment)
    vi.mocked(updateEquipment).mockResolvedValue(equipment)
    vi.mocked(setEquipmentAvailability).mockResolvedValue(equipment)
    vi.mocked(deleteEquipment).mockResolvedValue(undefined)
    vi.stubGlobal('confirm', vi.fn(() => true))
  })

  it('shows loading and empty states, then requests the caller-owned first page', async () => {
    render(<EquipmentTab accessToken="token" />)

    expect(await screen.findByText('No equipment yet. Add your first kitchen item.')).toBeTruthy()
    expect(listEquipment).toHaveBeenCalledWith('token', { page: 1, pageSize: 20 })
  })

  it('validates required fields and prevents submitting an empty item', async () => {
    render(<EquipmentTab accessToken="token" />)
    fireEvent.click(screen.getByRole('button', { name: 'Add equipment' }))
    fireEvent.click(screen.getByRole('button', { name: 'Save equipment' }))

    expect((await screen.findByRole('alert')).textContent).toContain('Name is required.')
    expect(createEquipment).not.toHaveBeenCalled()
  })

  it('creates a typed equipment DTO and refreshes the list', async () => {
    render(<EquipmentTab accessToken="token" />)
    fireEvent.click(screen.getByRole('button', { name: 'Add equipment' }))
    fireEvent.change(screen.getByLabelText('Name'), { target: { value: '  My pan  ' } })
    fireEvent.change(screen.getByLabelText('Quantity'), { target: { value: '3' } })
    fireEvent.change(screen.getByLabelText('Brand (optional)'), { target: { value: 'Acme' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save equipment' }))

    await waitFor(() => expect(createEquipment).toHaveBeenCalledWith('token', {
      name: 'My pan',
      type: 'Cookware',
      quantity: 3,
      condition: 'Good',
      brand: 'Acme',
      size: null,
      notes: null,
      location: null,
    }, expect.any(String)))
    expect(await screen.findByText('Equipment added.')).toBeTruthy()
    expect(listEquipment).toHaveBeenCalledTimes(2)
  })

  it('reuses the idempotency key when retrying an identical failed create', async () => {
    vi.mocked(createEquipment)
      .mockRejectedValueOnce(new Error('network'))
      .mockResolvedValueOnce(equipment)
    render(<EquipmentTab accessToken="token" />)
    fireEvent.click(screen.getByRole('button', { name: 'Add equipment' }))
    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Stock pot' } })

    fireEvent.click(screen.getByRole('button', { name: 'Save equipment' }))
    await waitFor(() => expect(createEquipment).toHaveBeenCalledTimes(1))
    await waitFor(() => expect(screen.getByRole('button', { name: 'Save equipment' }).hasAttribute('disabled')).toBe(false))
    fireEvent.click(screen.getByRole('button', { name: 'Save equipment' }))
    await waitFor(() => expect(createEquipment).toHaveBeenCalledTimes(2))

    const keys = vi.mocked(createEquipment).mock.calls.map((call) => call[2])
    expect(keys[0]).toBeTruthy()
    expect(keys[1]).toBe(keys[0])
  })

  it('disables the submit action while a create request is in flight', async () => {
    let finishCreate: ((item: Equipment) => void) | undefined
    vi.mocked(createEquipment).mockImplementation(
      () => new Promise((resolve) => { finishCreate = resolve }),
    )
    render(<EquipmentTab accessToken="token" />)
    fireEvent.click(screen.getByRole('button', { name: 'Add equipment' }))
    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Stock pot' } })
    const submit = screen.getByRole('button', { name: 'Save equipment' })

    fireEvent.click(submit)
    await waitFor(() => expect(submit.hasAttribute('disabled')).toBe(true))
    fireEvent.click(submit)
    expect(createEquipment).toHaveBeenCalledTimes(1)

    finishCreate?.(equipment)
    await screen.findByText('Equipment added.')
  })

  it('applies search and filters and resets pagination', async () => {
    render(<EquipmentTab accessToken="token" />)
    await screen.findByText('No equipment yet. Add your first kitchen item.')
    fireEvent.change(screen.getByLabelText('Search'), { target: { value: 'pan' } })
    fireEvent.change(screen.getByLabelText('Type'), { target: { value: 'Cookware' } })
    fireEvent.change(screen.getByLabelText('Condition'), { target: { value: 'Good' } })
    fireEvent.change(screen.getByLabelText('Availability'), { target: { value: 'Available' } })
    fireEvent.click(screen.getByRole('button', { name: 'Apply filters' }))

    await waitFor(() => expect(listEquipment).toHaveBeenLastCalledWith('token', {
      page: 1,
      pageSize: 20,
      search: 'pan',
      type: 'Cookware',
      condition: 'Good',
      availability: 'Available',
    }))
  })

  it('moves through server pagination', async () => {
    vi.mocked(listEquipment).mockResolvedValue({
      items: [equipment],
      page: 1,
      pageSize: 20,
      totalCount: 21,
    })
    render(<EquipmentTab accessToken="token" />)
    await screen.findByRole('heading', { name: 'Saucepan' })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))

    await waitFor(() => expect(listEquipment).toHaveBeenLastCalledWith('token', {
      page: 2,
      pageSize: 20,
    }))
  })

  it('changes availability using the current item version', async () => {
    vi.mocked(listEquipment).mockResolvedValue({
      items: [equipment],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    })
    render(<EquipmentTab accessToken="token" />)
    await screen.findByRole('heading', { name: 'Saucepan' })
    fireEvent.click(screen.getByRole('button', { name: 'Mark unavailable' }))

    await waitFor(() => expect(setEquipmentAvailability).toHaveBeenCalledWith(
      'token',
      equipment.id,
      equipment.version,
      false,
    ))
  })

  it('confirms deletion before sending the conditional delete', async () => {
    vi.mocked(listEquipment).mockResolvedValue({
      items: [equipment],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    })
    render(<EquipmentTab accessToken="token" />)
    await screen.findByRole('heading', { name: 'Saucepan' })
    fireEvent.click(screen.getByRole('button', { name: 'Delete' }))

    await waitFor(() => expect(deleteEquipment).toHaveBeenCalledWith('token', equipment))
  })
})
