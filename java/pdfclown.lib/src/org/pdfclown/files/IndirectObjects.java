/*
  Copyright 2006-2011 Stefano Chizzolini. http://www.pdfclown.org

  Contributors:
    * Stefano Chizzolini (original code developer, http://www.stefanochizzolini.it)

  This file should be part of the source code distribution of "PDF Clown library"
  (the Program): see the accompanying README files for more info.

  This Program is free software; you can redistribute it and/or modify it under the terms
  of the GNU Lesser General Public License as published by the Free Software Foundation;
  either version 3 of the License, or (at your option) any later version.

  This Program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY,
  either expressed or implied; without even the implied warranty of MERCHANTABILITY or
  FITNESS FOR A PARTICULAR PURPOSE. See the License for more details.

  You should have received a copy of the GNU Lesser General Public License along with this
  Program (see README files); if not, go to the GNU website (http://www.gnu.org/licenses/).

  Redistribution and use, with or without modification, are permitted provided that such
  redistributions retain the above copyright notice, license and disclaimer, along with
  this list of conditions.
*/

package org.pdfclown.files;

import java.util.ArrayList;
import java.util.Collection;
import java.util.Hashtable;
import java.util.Iterator;
import java.util.List;
import java.util.ListIterator;
import java.util.NoSuchElementException;
import java.util.SortedMap;
import java.util.TreeMap;

import org.pdfclown.objects.PdfDataObject;
import org.pdfclown.objects.PdfIndirectObject;
import org.pdfclown.tokens.XRefEntry;
import org.pdfclown.util.NotImplementedException;

/**
  Collection of the <b>alive indirect objects</b> available inside the file.
  <p>According to the PDF spec, <i>indirect object entries may be free
  (no data object associated) or in-use (data object associated)</i>.</p>
  <p>We can effectively subdivide indirect objects in two possibly-overriding
  collections: the <b>original indirect objects</b> (coming from the associated
  preexisting file) and the <b>newly-registered indirect objects</b> (coming
  from new data objects or original indirect objects manipulated during the
  current session).</p>
  <p><i>To ensure that the modifications applied to an original indirect object
  are committed to being persistent</i> is critical that the modified original
  indirect object is newly-registered (practically overriding the original
  indirect object).</p>
  <p><b>Alive indirect objects</b> encompass all the newly-registered ones plus
  not-overridden original ones.</p>

  @author Stefano Chizzolini (http://www.stefanochizzolini.it)
  @since 0.0.0
  @version 0.1.1, 11/01/11
*/
public final class IndirectObjects
  implements List<PdfIndirectObject>
{
  // <class>
  // <dynamic>
  // <fields>
  /**
    Associated file.
  */
  private final File file;

  /**
    Map of matching references of imported indirect objects.
    <p>This collection is used to prevent duplications among imported indirect
    objects.</p>
    <p><code>Key</code> is the external indirect object hashcode, <code>Value</code> is the
    matching internal indirect object.</p>
  */
  private final Hashtable<Integer,PdfIndirectObject> importedObjects = new Hashtable<Integer,PdfIndirectObject>();
  /**
    Collection of newly-registered indirect objects.
  */
  private final TreeMap<Integer,PdfIndirectObject> modifiedObjects = new TreeMap<Integer,PdfIndirectObject>();
  /**
    Collection of instantiated original indirect objects.
    <p>This collection is used as a cache to avoid unconsistent parsing duplications.</p>
  */
  private final TreeMap<Integer,PdfIndirectObject> wokenObjects = new TreeMap<Integer,PdfIndirectObject>();

  /**
    Object counter.
  */
  private int lastObjectNumber;
  /**
    Offsets of the original indirect objects inside the associated file (to say:
    implicit collection of the original indirect objects).
    <p>This information is vital to randomly retrieve the indirect-object persistent
    representation inside the associated file.</p>
  */
  private final SortedMap<Integer,XRefEntry> xrefEntries;
  // </fields>

  // <constructors>
  IndirectObjects(
    File file,
    SortedMap<Integer,XRefEntry> xrefEntries
    )
  {
    this.file = file;
    this.xrefEntries = xrefEntries;
    if(this.xrefEntries == null) // No original indirect objects.
    {
      // Register the leading free-object!
      /*
        NOTE: Mandatory head of the linked list of free objects
        at object number 0 [PDF:1.6:3.4.3].
      */
      lastObjectNumber = 0;
      modifiedObjects.put(
        lastObjectNumber,
        new PdfIndirectObject(
          this.file,
          null,
          new XRefEntry(
            lastObjectNumber,
            XRefEntry.GenerationUnreusable,
            0,
            XRefEntry.UsageEnum.Free
            )
          )
        );
    }
    else
    {
      // Adjust the object counter!
      lastObjectNumber = xrefEntries.lastKey();
    }
  }
  // </constructors>

  // <interface>
  // <public>
  public File getFile(
    )
  {return file;}

  /**
    Register an <b>internal data object</b>.
    <p>Alternatives:</p>
    <ul>
      <li>To register a modified internal indirect object, use
      {@link #set(int,PdfIndirectObject) set(int,PdfIndirectObject)}.</li>
      <li>To register an external indirect object, use
      {@link #addExternal(PdfIndirectObject) addExternal(PdfIndirectObject)}.</li>
    </ul>
  */
  public PdfIndirectObject add(
    PdfDataObject object
    )
  {
    // Wrap the data object inside a new indirect object!
    lastObjectNumber++;
    PdfIndirectObject indirectObject = new PdfIndirectObject(
      file,
      object,
      new XRefEntry(lastObjectNumber, 0)
      );
    // Register the object!
    modifiedObjects.put(lastObjectNumber,indirectObject);
    return indirectObject;
  }

  /**
    Registers and gets an <b>external indirect object</b>.
    <p>External indirect objects come from alien PDF files. <i>This is a powerful
    way to import the content of one file into another</i>.</p>
    <p>Alternatives:</p>
    <ul>
      <li>To register a modified internal indirect object, use
      {@link #set(int,PdfIndirectObject) set(int,PdfIndirectObject)}.</li>
      <li>To register an internal data object, use
      {@link #add(PdfDataObject) add(PdfDataObject)}.</li>
    </ul>
  */
  public PdfIndirectObject addExternal(
    PdfIndirectObject object
    )
  {
    PdfIndirectObject indirectObject = importedObjects.get(object.hashCode());
    // Hasn't the external indirect object been imported yet?
    if(indirectObject == null)
    {
      // Keep track of the imported indirect object!
      importedObjects.put(
        object.hashCode(),
        indirectObject = add((PdfDataObject)object.getDataObject().clone(file)) // Registers the clone of the data object corresponding to the external indirect object.
        );
    }
    return indirectObject;
  }

  public Collection<? extends PdfIndirectObject> addAllExternal(
    Collection<? extends PdfIndirectObject> objects
    )
  {
    ArrayList<PdfIndirectObject> addedObjects = new ArrayList<PdfIndirectObject>(objects.size());
    for(PdfIndirectObject object : objects)
    {addedObjects.add(addExternal(object));}

    return addedObjects;
  }

  // <List>
  @Override
  public void add(
    int index,
    PdfIndirectObject object
    )
  {throw new UnsupportedOperationException();}

  @Override
  public boolean addAll(
    int index,
    Collection<? extends PdfIndirectObject> objects
    )
  {throw new UnsupportedOperationException();}

  /**
    Gets an indirect object with the specified object number.

    @param index Object number of the indirect object to get.
    @return Indirect object corresponding to the specified object number.
  */
  @Override
  public PdfIndirectObject get(
    int index
    )
  {
    PdfIndirectObject object = modifiedObjects.get(index);
    if(object == null)
    {
      object = wokenObjects.get(index);
      if(object == null)
      {
        XRefEntry xrefEntry = xrefEntries.get(index);
        if(xrefEntry == null)
        {
          if(index > lastObjectNumber)
            return null;

          /*
            NOTE: The cross-reference table (comprising the original cross-reference section and all update sections)
            MUST contain one entry for each object number from 0 to the maximum object number used in the file, even
            if one or more of the object numbers in this range do not actually occur in the file.
            However, for resilience purposes missing entries are treated as free ones.
          */
          xrefEntries.put(
            index,
            xrefEntry = new XRefEntry(
              index,
              XRefEntry.GenerationUnreusable,
              0,
              XRefEntry.UsageEnum.Free
              )
            );
        }

        // Awake the object!
        /*
          NOTE: This operation allows to keep a consistent state across the whole session,
          avoiding multiple incoherent instantiations of the same original indirect object.
        */
        wokenObjects.put(index, object = new PdfIndirectObject(file, null, xrefEntry));
      }
    }
    return object;
  }

  @Override
  public int indexOf(
    Object object
    )
  {
    // Is this indirect object associated to this file?
    if(((PdfIndirectObject)object).getFile() != file)
      return -1;

    return ((PdfIndirectObject)object).getReference().getObjectNumber();
  }

  @Override
  public int lastIndexOf(
    Object object
    )
  {
    /*
      NOTE: By definition, there's a bijective relation between indirect objects
      and their indices.
    */
    return indexOf(object);
  }

  @Override
  public ListIterator<PdfIndirectObject> listIterator(
    )
  {throw new NotImplementedException();}

  @Override
  public ListIterator<PdfIndirectObject> listIterator(
    int index
    )
  {throw new NotImplementedException();}

  @Override
  public PdfIndirectObject remove(
    int index
    )
  {
    /*
      NOTE: Acrobat 6.0 and later (PDF 1.5+) DO NOT use the free list to recycle object numbers;
      new objects are assigned new numbers [PDF:1.6:H.3:16].
      According to such an implementation note, we simply mark the removed object as 'not-reusable'
      newly-freed entry, neglecting both to add it to the linked list of free entries
      and to increment by 1 its generation number.
    */
    return update(
      new PdfIndirectObject(
        file,
        null,
        new XRefEntry(
          index,
          XRefEntry.GenerationUnreusable,
          0,
          XRefEntry.UsageEnum.Free
          )
        )
      );
  }

  /**
    Sets an indirect object with the specified object number.
    <p>This method is currently limited to <b>internal indirect
    objects</b>: <i>use it to register modified internal indirect objects only</i>.
    If you need to register alternative-type objects, consider the following
    methods:</p>
    <ul>
      <li>to register an <b>external indirect object</b>, use
      {@link #addExternal(PdfIndirectObject) addExternal(PdfIndirectObject)}.</li>
      <li>to register an <b>internal data object</b>, use
      {@link #add(PdfDataObject) add(PdfDataObject)}.</li>
    </ul>

    @param index Object number of the indirect object to set.
    @param object Indirect object to set.
    @return Replaced indirect object.
  */
  @Override
  public PdfIndirectObject set(
    int index,
    PdfIndirectObject object
    )
  {throw new UnsupportedOperationException();}

  @Override
  public List<PdfIndirectObject> subList(
    int fromIndex,
    int toIndex
    )
  {throw new NotImplementedException();}

  // <Collection>
  /**
    Registers an <b>external indirect object</b>.
    <p>External indirect objects come from alien PDF files. <i>This is a powerful
    way to import the content of one file into another</i>.</p>
    <p>Alternatives:</p>
    <ul>
      <li>To register and get an external indirect object, use
      {@link #addExternal(PdfIndirectObject) addExternal(PdfIndirectObject)}.</li>
    </ul>
  */
  @Override
  public boolean add(
    PdfIndirectObject object
    )
  {return (addExternal(object) != null);}

  /**
    Registers <b>external indirect objects</b>.
    <p>External indirect objects come from alien PDF files. <i>This is a powerful
    way to import the content of one file into another</i>.</p>
    <p>Alternatives:</p>
    <ul>
      <li>To register and get external indirect object, use
      {@link #addAllExternal(Collection<? extends PdfIndirectObject>) addAllExternal(Collection<? extends PdfIndirectObject>)}.</li>
    </ul>
  */
  @Override
  public boolean addAll(
    Collection<? extends PdfIndirectObject> objects
    )
  {
    boolean changed = false;
    for(PdfIndirectObject object : objects)
    {changed |= (addExternal(object) != null);}
    return changed;
  }

  @Override
  public void clear(
    )
  {throw new UnsupportedOperationException();}

  @Override
  public boolean contains(
    Object object
    )
  {throw new NotImplementedException();}

  @Override
  public boolean containsAll(
    Collection<?> objects
    )
  {throw new NotImplementedException();}

  @Override
  public boolean equals(
    Object object
    )
  {throw new NotImplementedException();}

  @Override
  public int hashCode(
    )
  {throw new NotImplementedException();}

  @Override
  public boolean isEmpty(
    )
  {
    /*
      NOTE: Semantics of the indirect objects collection imply that the collection is considered
      empty in any case no in-use object is available.
    */
    for(PdfIndirectObject object : this)
    {
      if(object.isInUse())
        return false;
    }
    return true;
  }

  @Override
  public boolean remove(
    Object object
    )
  {
    return (remove(
      ((PdfIndirectObject)object).getReference().getObjectNumber()
      ) != null);
  }

  @Override
  public boolean removeAll(
    Collection<?> objects
    )
  {throw new NotImplementedException();}

  @Override
  public boolean retainAll(
    Collection<?> objects
    )
  {throw new UnsupportedOperationException();}

  /**
    Gets the number of entries available (both in-use and free) in the
    collection.

    @return The number of entries available in the collection.
  */
  @Override
  public int size(
    )
  {return (lastObjectNumber + 1);}

  @Override
  public PdfIndirectObject[] toArray(
    )
  {throw new NotImplementedException();}

  @Override
  public <T> T[] toArray(
    T[] objects
    )
  {throw new NotImplementedException();}

  // <Iterable>
  @Override
  public Iterator<PdfIndirectObject> iterator(
    )
  {
    return new Iterator<PdfIndirectObject>()
    {
      /** Index of the next item. */
      private int index = 0;

      @Override
      public boolean hasNext(
        )
      {return (index < size());}

      @Override
      public PdfIndirectObject next(
        )
      {
        if(!hasNext())
          throw new NoSuchElementException();

        return get(index++);
      }

      @Override
      public void remove(
        )
      {throw new UnsupportedOperationException();}
    };
  }
  // </Iterable>
  // </Collection>
  // </List>
  // </public>

  // <internal>
  /**
    <span style="color:red">For internal use only.</span>
  */
  public TreeMap<Integer,PdfIndirectObject> getModifiedObjects(
    )
  {return modifiedObjects;}

  /**
    <span style="color:red">For internal use only.</span>
  */
  public PdfIndirectObject update(
    PdfIndirectObject object
    )
  {
    int index = object.getReference().getObjectNumber();

    // Get the old indirect object to be replaced!
    PdfIndirectObject old = get(index);
    if(old != object)
    {old.dropFile();} // Disconnects the old indirect object.

    // Insert the new indirect object into the modified objects collection!
    modifiedObjects.put(index,object);
    // Remove old indirect object from cache!
    wokenObjects.remove(index);
    // Mark the new indirect object as modified!
    object.dropOriginal();

    return old;
  }
  // </internal>
  // </interface>
  // </dynamic>
  // </class>
}