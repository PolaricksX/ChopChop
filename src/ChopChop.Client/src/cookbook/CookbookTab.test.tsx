import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { CookbookTab } from './CookbookTab'
import * as api from './api'
import { getRecipeVersion } from '../recipes/api'

vi.mock('./api')
vi.mock('../recipes/api', () => ({ getRecipeVersion: vi.fn() }))

describe('CookbookTab', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(api.listFolders).mockResolvedValue([])
    vi.mocked(api.createFolder).mockResolvedValue({ id: 'new', name: 'Dinner', createdAtUtc: '' })
  })

  it('shows the empty state and validates folder names', async () => {
    render(<CookbookTab />)
    expect(await screen.findByText('No folders yet. Create one to save recipes.')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Create folder' }))
    expect(screen.getByRole('alert').textContent).toContain('Folder name is required.')
    fireEvent.change(screen.getByLabelText('New folder'), { target: { value: 'Dinner' } })
    fireEvent.click(screen.getByRole('button', { name: 'Create folder' }))
    await waitFor(() => expect(api.createFolder).toHaveBeenCalledWith('Dinner'))
  })

  it('shows the recipe title when the lookup succeeds', async () => {
    vi.mocked(api.listFolders).mockResolvedValue([{ id: 'folder', name: 'Favorites', createdAtUtc: '' }])
    vi.mocked(api.listSavedRecipes).mockResolvedValue([
      { folderId: 'folder', recipeId: 'known', recipeVersion: 2, savedAtUtc: '' },
    ])
    vi.mocked(getRecipeVersion).mockResolvedValue({ version: { title: 'Pasta' } } as never)
    render(<CookbookTab />)
    fireEvent.click(await screen.findByRole('button', { name: 'Favorites' }))
    expect(await screen.findByText('Pasta')).toBeTruthy()
  })

  it('shows the recipe ID and version when the lookup fails', async () => {
    vi.mocked(api.listFolders).mockResolvedValue([{ id: 'folder', name: 'Favorites', createdAtUtc: '' }])
    vi.mocked(api.listSavedRecipes).mockResolvedValue([
      { folderId: 'folder', recipeId: 'missing', recipeVersion: 3, savedAtUtc: '' },
    ])
    vi.mocked(getRecipeVersion).mockRejectedValue(new Error('not found'))
    render(<CookbookTab />)
    fireEvent.click(await screen.findByRole('button', { name: 'Favorites' }))
    expect(await screen.findByText('Recipe missing (version 3)')).toBeTruthy()
    expect(screen.queryByRole('alert')).toBeNull()
  })
})
