import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { PantryTab } from './PantryTab'

const getAccessToken = vi.fn().mockResolvedValue('pantry-token')

describe('PantryTab', () => {
  beforeEach(() => {
    getAccessToken.mockReset().mockResolvedValue('pantry-token')
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify([]))))
  })

  it('loads the caller pantry using the read scope', async () => {
    render(<PantryTab getAccessToken={getAccessToken} />)

    expect(await screen.findByText('No pantry items match these filters.')).toBeInTheDocument()
    expect(getAccessToken).toHaveBeenCalledWith('pantry.read')
  })

  it('creates a pantry item using the write scope and refreshes the list', async () => {
    const fetcher = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify([])))
      .mockResolvedValueOnce(new Response(JSON.stringify({ id: 'item-1' }), { status: 201 }))
      .mockResolvedValueOnce(new Response(JSON.stringify([{ id: 'item-1', name: 'Rice', type: 'Grains', quantity: 2, unit: 'Cup' }])))
    vi.stubGlobal('fetch', fetcher)
    render(<PantryTab getAccessToken={getAccessToken} />)

    fireEvent.click(await screen.findByRole('button', { name: 'Add item' }))
    fireEvent.change(screen.getByLabelText('Name'), { target: { value: ' Rice ' } })
    fireEvent.change(screen.getByLabelText('Quantity'), { target: { value: '2' } })
    fireEvent.change(screen.getByLabelText('Unit'), { target: { value: 'Cup' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save item' }))

    expect(await screen.findByText('Pantry item added.')).toBeInTheDocument()
    expect(await screen.findByRole('heading', { name: 'Rice' })).toBeInTheDocument()
    await waitFor(() => expect(getAccessToken).toHaveBeenCalledWith('pantry.write'))
    expect(JSON.parse(fetcher.mock.calls[1][1].body)).toMatchObject({
      name: 'Rice',
      type: 'Meat',
      quantity: 2,
      unit: 'Cup',
      expiresAt: null,
    })
  })
})
