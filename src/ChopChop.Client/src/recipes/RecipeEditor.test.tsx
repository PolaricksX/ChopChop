import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { RecipeEditor, validateRecipe } from './RecipeEditor'
import { RecipeDetails } from './RecipeDetails'
import type { RecipeContent } from './types'

const valid: RecipeContent = { title: 'Soup', description: 'Warm soup', servings: 2, ingredients: [{ name: 'Tomato', quantity: 1, unit: 'piece' }], steps: [{ order: 1, content: 'Cook it' }], allergenStatus: 'Declared', allergens: [], tags: [], dietaryLabels: [], equipment: [], prepMinutes: 5, cookMinutes: 10, totalMinutes: 15, isAiGenerated: false }
describe('RecipeEditor', () => {
  it('reports required recipe fields before submitting', () => {
    expect(validateRecipe({ ...valid, title: '', ingredients: [] })).toEqual(expect.arrayContaining(['Title is required.', 'Add an ingredient with a positive quantity, name, and unit.']))
  })
  it('shows client validation errors in the editor', () => {
    vi.stubGlobal('fetch', vi.fn())
    render(<RecipeEditor accessToken="token" />)
    fireEvent.click(screen.getByRole('button', { name: 'Save draft' }))
    expect(screen.getByRole('alert').textContent).toContain('Title is required.')
  })
  it('displays a readable API problem detail', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ detail: 'Recipe was not found.' }), { status: 404 })))
    render(<RecipeDetails recipeId="missing" />)
    expect((await screen.findByRole('alert')).textContent).toContain('Recipe was not found.')
  })
})
